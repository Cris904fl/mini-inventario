-- =============================================================================
-- Mini sistema de gestion de inventario - esquema, procedimientos y datos
-- PostgreSQL 16
--
-- Como ejecutarlo:
--   psql -U <usuario> -d <base> -f database/inventario.sql
-- Con docker compose se ejecuta solo al crear el volumen por primera vez.
--
-- Decisiones de diseno (se explican tambien en el README):
--   * La cantidad vive en productos.cantidad y SOLO la modifican los
--     procedimientos sp_registrar_entrada / sp_registrar_salida, que ademas
--     insertan el movimiento en la misma transaccion. Asi el inventario y el
--     historial nunca se desincronizan.
--   * Las lecturas (inventario actual, historial) son FUNCIONES, no
--     procedimientos: en PostgreSQL un PROCEDURE no devuelve filas, y una
--     funcion RETURNS TABLE se consume con un SELECT normal.
--   * Los errores de negocio salen con SQLSTATE propio (IN001, IN002, IN003)
--     para que la API los traduzca a 409/404/400 sin leer el texto del mensaje.
-- =============================================================================

BEGIN;

CREATE EXTENSION IF NOT EXISTS pgcrypto;   -- crypt() / gen_salt() para la clave del usuario semilla

-- Permite volver a ejecutar el script sobre una base ya creada.
DROP FUNCTION  IF EXISTS fn_historial_movimientos(INT, VARCHAR, INT, INT);
DROP FUNCTION  IF EXISTS fn_inventario_actual(TEXT, BOOLEAN);
DROP PROCEDURE IF EXISTS sp_crear_producto(VARCHAR, VARCHAR, TEXT, VARCHAR, INT, INT, VARCHAR, INT);
DROP PROCEDURE IF EXISTS sp_registrar_salida(INT, INT, VARCHAR, TEXT, BIGINT, INT);
DROP PROCEDURE IF EXISTS sp_registrar_entrada(INT, INT, VARCHAR, TEXT, BIGINT, INT);
DROP TABLE IF EXISTS movimientos;
DROP TABLE IF EXISTS productos;
DROP TABLE IF EXISTS usuarios;

-- -----------------------------------------------------------------------------
-- Tablas
-- -----------------------------------------------------------------------------

CREATE TABLE usuarios (
    id              SERIAL PRIMARY KEY,
    nombre_usuario  VARCHAR(50)  NOT NULL UNIQUE,
    nombre_completo VARCHAR(120) NOT NULL,
    clave_hash      TEXT         NOT NULL,          -- bcrypt; nunca la clave en claro
    activo          BOOLEAN      NOT NULL DEFAULT TRUE,
    creado_en       TIMESTAMPTZ  NOT NULL DEFAULT now()
);

CREATE TABLE productos (
    id             SERIAL PRIMARY KEY,
    codigo         VARCHAR(20)  NOT NULL UNIQUE,
    nombre         VARCHAR(120) NOT NULL,
    descripcion    TEXT,
    unidad         VARCHAR(20)  NOT NULL DEFAULT 'UND',
    stock_minimo   INT          NOT NULL DEFAULT 0 CHECK (stock_minimo >= 0),
    cantidad       INT          NOT NULL DEFAULT 0 CHECK (cantidad >= 0),  -- ultima barrera: nunca negativo
    activo         BOOLEAN      NOT NULL DEFAULT TRUE,
    creado_en      TIMESTAMPTZ  NOT NULL DEFAULT now(),
    actualizado_en TIMESTAMPTZ  NOT NULL DEFAULT now()
);

CREATE TABLE movimientos (
    id               BIGSERIAL PRIMARY KEY,
    producto_id      INT         NOT NULL REFERENCES productos (id),
    tipo             VARCHAR(10) NOT NULL CHECK (tipo IN ('ENTRADA', 'SALIDA')),
    cantidad         INT         NOT NULL CHECK (cantidad > 0),
    stock_resultante INT         NOT NULL CHECK (stock_resultante >= 0),  -- foto del stock tras el movimiento (kardex)
    usuario          VARCHAR(50) NOT NULL,
    observacion      TEXT,
    fecha            TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX ix_movimientos_producto_fecha ON movimientos (producto_id, fecha DESC);
CREATE INDEX ix_movimientos_fecha          ON movimientos (fecha DESC);

COMMENT ON COLUMN productos.cantidad IS
    'Stock actual. Solo lo modifican sp_registrar_entrada y sp_registrar_salida.';
COMMENT ON COLUMN movimientos.stock_resultante IS
    'Cantidad que quedo en el producto justo despues de este movimiento.';

-- -----------------------------------------------------------------------------
-- Procedimientos de escritura
--
-- Devuelven el id del movimiento y el stock resultante por parametros INOUT:
-- CALL sp_registrar_entrada(1, 10, 'admin', NULL, NULL, NULL) responde una fila.
-- -----------------------------------------------------------------------------

CREATE OR REPLACE PROCEDURE sp_registrar_entrada(
    IN    p_producto_id      INT,
    IN    p_cantidad         INT,
    IN    p_usuario          VARCHAR(50),
    IN    p_observacion      TEXT   DEFAULT NULL,
    INOUT p_movimiento_id    BIGINT DEFAULT NULL,
    INOUT p_stock_resultante INT    DEFAULT NULL
)
LANGUAGE plpgsql
AS $$
BEGIN
    IF p_cantidad IS NULL OR p_cantidad <= 0 THEN
        RAISE EXCEPTION 'La cantidad debe ser mayor que cero'
            USING ERRCODE = 'IN003';
    END IF;

    -- FOR UPDATE bloquea la fila hasta el COMMIT: dos entradas simultaneas del
    -- mismo producto se serializan y ninguna pisa la suma de la otra.
    SELECT cantidad INTO p_stock_resultante
    FROM productos
    WHERE id = p_producto_id AND activo
    FOR UPDATE;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'El producto % no existe o esta inactivo', p_producto_id
            USING ERRCODE = 'IN002';
    END IF;

    p_stock_resultante := p_stock_resultante + p_cantidad;

    UPDATE productos
    SET cantidad = p_stock_resultante,
        actualizado_en = now()
    WHERE id = p_producto_id;

    INSERT INTO movimientos (producto_id, tipo, cantidad, stock_resultante, usuario, observacion)
    VALUES (p_producto_id, 'ENTRADA', p_cantidad, p_stock_resultante, p_usuario, NULLIF(TRIM(p_observacion), ''))
    RETURNING id INTO p_movimiento_id;
END;
$$;

CREATE OR REPLACE PROCEDURE sp_registrar_salida(
    IN    p_producto_id      INT,
    IN    p_cantidad         INT,
    IN    p_usuario          VARCHAR(50),
    IN    p_observacion      TEXT   DEFAULT NULL,
    INOUT p_movimiento_id    BIGINT DEFAULT NULL,
    INOUT p_stock_resultante INT    DEFAULT NULL
)
LANGUAGE plpgsql
AS $$
DECLARE
    v_stock_actual INT;
BEGIN
    IF p_cantidad IS NULL OR p_cantidad <= 0 THEN
        RAISE EXCEPTION 'La cantidad debe ser mayor que cero'
            USING ERRCODE = 'IN003';
    END IF;

    -- El bloqueo es lo que hace valida la comprobacion de stock: entre el
    -- SELECT y el UPDATE nadie mas puede descontar de esta fila.
    SELECT cantidad INTO v_stock_actual
    FROM productos
    WHERE id = p_producto_id AND activo
    FOR UPDATE;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'El producto % no existe o esta inactivo', p_producto_id
            USING ERRCODE = 'IN002';
    END IF;

    -- Regla de negocio central: no se puede sacar mas de lo que hay.
    IF v_stock_actual < p_cantidad THEN
        RAISE EXCEPTION 'Stock insuficiente: hay % y se intentan retirar %', v_stock_actual, p_cantidad
            USING ERRCODE = 'IN001',
                  DETAIL  = format('{"disponible":%s,"solicitado":%s}', v_stock_actual, p_cantidad);
    END IF;

    p_stock_resultante := v_stock_actual - p_cantidad;

    UPDATE productos
    SET cantidad = p_stock_resultante,
        actualizado_en = now()
    WHERE id = p_producto_id;

    INSERT INTO movimientos (producto_id, tipo, cantidad, stock_resultante, usuario, observacion)
    VALUES (p_producto_id, 'SALIDA', p_cantidad, p_stock_resultante, p_usuario, NULLIF(TRIM(p_observacion), ''))
    RETURNING id INTO p_movimiento_id;
END;
$$;

-- Alta de producto. Si trae cantidad inicial, se registra como ENTRADA para que
-- el historial explique de donde salio cada unidad.
CREATE OR REPLACE PROCEDURE sp_crear_producto(
    IN    p_codigo           VARCHAR(20),
    IN    p_nombre           VARCHAR(120),
    IN    p_descripcion      TEXT        DEFAULT NULL,
    IN    p_unidad           VARCHAR(20) DEFAULT 'UND',
    IN    p_stock_minimo     INT         DEFAULT 0,
    IN    p_cantidad_inicial INT         DEFAULT 0,
    IN    p_usuario          VARCHAR(50) DEFAULT 'sistema',
    INOUT p_producto_id      INT         DEFAULT NULL
)
LANGUAGE plpgsql
AS $$
DECLARE
    -- Dentro de PL/pgSQL los parametros INOUT de un CALL anidado deben ser
    -- variables (no se pueden omitir ni pasar NULL literal).
    v_movimiento_id    BIGINT;
    v_stock_resultante INT;
BEGIN
    IF p_codigo IS NULL OR TRIM(p_codigo) = '' OR p_nombre IS NULL OR TRIM(p_nombre) = '' THEN
        RAISE EXCEPTION 'El codigo y el nombre del producto son obligatorios'
            USING ERRCODE = 'IN003';
    END IF;

    IF p_cantidad_inicial IS NULL OR p_cantidad_inicial < 0 THEN
        RAISE EXCEPTION 'La cantidad inicial no puede ser negativa'
            USING ERRCODE = 'IN003';
    END IF;

    INSERT INTO productos (codigo, nombre, descripcion, unidad, stock_minimo)
    VALUES (UPPER(TRIM(p_codigo)), TRIM(p_nombre), NULLIF(TRIM(p_descripcion), ''),
            COALESCE(NULLIF(TRIM(p_unidad), ''), 'UND'), COALESCE(p_stock_minimo, 0))
    RETURNING id INTO p_producto_id;
    -- Un codigo repetido lo rechaza la restriccion UNIQUE (SQLSTATE 23505).

    IF p_cantidad_inicial > 0 THEN
        CALL sp_registrar_entrada(p_producto_id, p_cantidad_inicial, p_usuario, 'Inventario inicial',
                                  v_movimiento_id, v_stock_resultante);
    END IF;
END;
$$;

-- -----------------------------------------------------------------------------
-- Funciones de consulta (estado actual del inventario e historial)
-- -----------------------------------------------------------------------------

CREATE OR REPLACE FUNCTION fn_inventario_actual(
    p_busqueda  TEXT    DEFAULT NULL,   -- filtra por codigo o nombre (sin distinguir mayusculas)
    p_solo_bajo BOOLEAN DEFAULT FALSE   -- TRUE: solo productos en BAJO o AGOTADO
)
RETURNS TABLE (
    id                INT,
    codigo            VARCHAR(20),
    nombre            VARCHAR(120),
    descripcion       TEXT,
    unidad            VARCHAR(20),
    cantidad          INT,
    stock_minimo      INT,
    estado            TEXT,            -- DISPONIBLE | BAJO | AGOTADO
    ultimo_movimiento TIMESTAMPTZ
)
LANGUAGE sql
STABLE
AS $$
    SELECT p.id,
           p.codigo,
           p.nombre,
           p.descripcion,
           p.unidad,
           p.cantidad,
           p.stock_minimo,
           CASE
               WHEN p.cantidad = 0              THEN 'AGOTADO'
               WHEN p.cantidad <= p.stock_minimo THEN 'BAJO'
               ELSE 'DISPONIBLE'
           END AS estado,
           (SELECT MAX(m.fecha) FROM movimientos m WHERE m.producto_id = p.id) AS ultimo_movimiento
    FROM productos p
    WHERE p.activo
      AND (p_busqueda IS NULL OR TRIM(p_busqueda) = ''
           OR p.nombre ILIKE '%' || TRIM(p_busqueda) || '%'
           OR p.codigo ILIKE '%' || TRIM(p_busqueda) || '%')
      AND (NOT p_solo_bajo OR p.cantidad <= p.stock_minimo)
    ORDER BY p.nombre;
$$;

CREATE OR REPLACE FUNCTION fn_historial_movimientos(
    p_producto_id   INT         DEFAULT NULL,
    p_tipo          VARCHAR(10) DEFAULT NULL,   -- ENTRADA | SALIDA | NULL (todos)
    p_limite        INT         DEFAULT 20,
    p_desplazamiento INT        DEFAULT 0
)
RETURNS TABLE (
    id               BIGINT,
    fecha            TIMESTAMPTZ,
    producto_id      INT,
    codigo           VARCHAR(20),
    producto         VARCHAR(120),
    tipo             VARCHAR(10),
    cantidad         INT,
    stock_resultante INT,
    usuario          VARCHAR(50),
    observacion      TEXT,
    total            BIGINT           -- total de filas que cumplen el filtro, para paginar
)
LANGUAGE sql
STABLE
AS $$
    SELECT m.id,
           m.fecha,
           m.producto_id,
           p.codigo,
           p.nombre AS producto,
           m.tipo,
           m.cantidad,
           m.stock_resultante,
           m.usuario,
           m.observacion,
           COUNT(*) OVER () AS total
    FROM movimientos m
    JOIN productos p ON p.id = m.producto_id
    WHERE (p_producto_id IS NULL OR m.producto_id = p_producto_id)
      AND (p_tipo IS NULL OR m.tipo = UPPER(p_tipo))
    ORDER BY m.fecha DESC, m.id DESC
    LIMIT  GREATEST(COALESCE(p_limite, 20), 1)
    OFFSET GREATEST(COALESCE(p_desplazamiento, 0), 0);
$$;

-- -----------------------------------------------------------------------------
-- Datos iniciales
-- -----------------------------------------------------------------------------

-- Usuario de la aplicacion: admin / admin123 (bcrypt generado por pgcrypto;
-- la API lo verifica con BCrypt.Net).
INSERT INTO usuarios (nombre_usuario, nombre_completo, clave_hash)
VALUES ('admin', 'Administrador de bodega', crypt('admin123', gen_salt('bf', 10)));

-- Los productos entran por los mismos procedimientos que usa la API, de modo
-- que el historial cuadra con el stock desde el primer dia.
CALL sp_crear_producto('EST-120', 'Estiba de madera 120x100',        'Estiba estandar de 4 entradas',      'UND',   20, 150, 'admin');
CALL sp_crear_producto('CAJ-604', 'Caja de carton 60x40x40',         'Corrugado doble, para exportacion',  'UND',  100, 480, 'admin');
CALL sp_crear_producto('STR-500', 'Rollo de pelicula stretch 500 mm', 'Calibre 20, 300 m',                  'ROLLO', 10,  36, 'admin');
CALL sp_crear_producto('CIN-048', 'Cinta de embalaje 48 mm',         'Transparente, 100 m',                'ROLLO', 24,  60, 'admin');
CALL sp_crear_producto('ETQ-100', 'Etiqueta termica 100x150 mm',     'Rollo de 500 etiquetas',             'ROLLO', 15,  15, 'admin');
CALL sp_crear_producto('ZUN-016', 'Zuncho plastico 16 mm',           'Rollo de 1000 m',                    'ROLLO',  5,   8, 'admin');
CALL sp_crear_producto('GUA-NIT', 'Guantes de nitrilo',              'Talla M, caja de 100',               'CAJA',  10,  25, 'admin');
CALL sp_crear_producto('PRE-SEG', 'Precinto de seguridad numerado',  'Tipo cable, 3.5 mm',                 'UND',   50,  40, 'admin');

-- Movimientos de ejemplo: dejan un producto AGOTADO (PRE-SEG), varios en BAJO
-- (ETQ-100, ZUN-016, PRE-SEG) y el resto DISPONIBLE.
CALL sp_registrar_salida (1,  30, 'admin', 'Despacho contenedor MSKU-1234567');
CALL sp_registrar_salida (2, 120, 'admin', 'Pedido de empaque, cliente Andina');
CALL sp_registrar_entrada(3,  12, 'admin', 'Compra orden OC-2026-018');
CALL sp_registrar_salida (3,   6, 'admin', 'Consumo bodega 2');
CALL sp_registrar_salida (8,  40, 'admin', 'Sellado de contenedores semana 42');
CALL sp_registrar_salida (6,   3, 'admin', NULL);

COMMIT;
