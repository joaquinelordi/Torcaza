-- Script para la creacion de tablas de la base de datos de prueba 
-- TPP Torcaza

-- SECCION CONFIGURACION Y EXTENSIONES --
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";


-- SECCION CREATE TABLES --
DROP TABLE IF EXISTS "ubicacion"
CREATE TABLE IF NOT EXISTS "ubicacion" (
	"ubi_ID" serial NOT NULL,
    "ubi_registroID" uuid NOT NULL,
    "ubi_dispositivoID" uuid NOT NULL,
	"ubi_nroevento" bigint NOT NULL,
    "ubi_coordenadas" geometry(Point, 4326) NOT NULL, --SRID GPS
    "ubi_timestamp" timestamp with time zone NOT NULL,
    "ubi_hashusuario" varchar(255) NOT NULL,
    PRIMARY KEY ("ubi_ID"),
	CONSTRAINT "eventos_fk_ubicacion"
    FOREIGN KEY ("ubi_nroevento") REFERENCES "eventos"("eve_id"),
    CONSTRAINT unique_registro_dispositivo_timestamp
    UNIQUE ("ubi_registroID", "ubi_dispositivoID", "ubi_timestamp")
);
CREATE INDEX idx_ubicacion_coordenadas
ON ubicacion
USING GIST (ubi_coordenadas);
CREATE UNIQUE INDEX idx_ubi_registroid ON ubicacion("ubi_registroID");
CLUSTER ubicacion USING idx_ubi_registroid;

DROP TABLE IF EXISTS "dispositivos"
CREATE TABLE IF NOT EXISTS "dispositivos" (
    "disp_id" uuid NOT NULL UNIQUE,
    "disp_MACprimaria" varchar(255) NOT NULL UNIQUE,
    "disp_MACsecundaria" varchar(255) NOT NULL UNIQUE,
    "disp_habilitado" boolean,
    PRIMARY KEY ("disp_id")
);

DROP TABLE IF EXISTS "Usuarios"
CREATE TABLE IF NOT EXISTS "Usuarios" (
    "user_id" serial NOT NULL UNIQUE,
    "user_token" varchar(255) NOT NULL,
    "user_fechaIngreso" date NOT NULL,
    "user_activo" boolean NOT NULL,
    PRIMARY KEY ("user_id")
);

DROP TABLE IF EXISTS "dispositivosconfig";
CREATE TABLE IF NOT EXISTS "dispositivoconfig" (
    "dispoconf_id" uuid NOT NULL UNIQUE,
    "dispoconf_latencia" bigint NOT NULL,
    "dispoconf_mododefault" varchar(255) NOT NULL,
    "dispoconf_habilitado" bigint NOT NULL,
    PRIMARY KEY ("dispoconf_id")
);

DROP TABLE IF EXISTS "reportes_completos"
CREATE TABLE IF NOT EXISTS "reportes_completos" (
    "repc_dispid" uuid NOT NULL,
    "repc_regid" uuid NOT NULL,
    "repc_json" bigint NOT NULL,
    "repc_fecharecepcion" bigint NOT NULL,
    PRIMARY KEY ("repc_dispid", "repc_regid"),
    CONSTRAINT "reportes_completos_fk_disp"
    FOREIGN KEY ("repc_dispid") REFERENCES "dispositivos"("disp_id"),
    CONSTRAINT "reportes_completos_fk_ubicacion"
    FOREIGN KEY ("repc_regid") REFERENCES "ubicacion"("ubi_registroID")
);

DROP TABLE IF EXISTS "celdas_celulares";
CREATE TABLE IF NOT EXISTS "celdas_celulares" (
	"id" serial NOT NULL UNIQUE, -- id de la tabla
	"cell_nroevento" bigint NOT NULL,
	"cel_id" bigint, -- id de la celda
	"cel_mcc" bigint NOT NULL,
	"cel_mnc" bigint NOT NULL,
	"cel_lac" bigint NOT NULL,
	"cel_tecnologia" bigint,
	"cel_band" varchar(128),
	"cell_chanel" bigint,
	"cell_nivelsenial" bigint,
	"cell_timestamp" timestamp with time zone NOT NULL,
	"cell_reporte"	uuid NOT NULL, -- agrupa filas de celdas por paquete de mediciones reportado
	"cell_coordenadas" geometry(Point, 4326), --SRID GPS
	
	PRIMARY KEY ("id"),
	CONSTRAINT "eventos_fk_celdas_celulares"
    FOREIGN KEY ("cell_nroevento") REFERENCES "eventos"("eve_id")
);
CREATE INDEX idx_celdas_celulares_cell_reporte ON celdas_celulares(cell_reporte);

DROP TABLE IF EXISTS "reporte_celda_celular";
CREATE TABLE IF NOT EXISTS "reporte_celda_celular" (
	rcel_id SERIAL PRIMARY KEY,
	rcel_numeroreporte UUID NOT NULL,
	rcel_idregistro UUID NOT NULL,
	rcel_iddispositivo UUID NOT NULL,
	rcel_agenteid UUID NOT NULL,
	rcel_fecha TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT now(),
	
	CONSTRAINT uq_numero_reporte UNIQUE (rcel_numeroreporte)
);

-- almacena eventos de recepcion de mensajes enviados por dispositivos al servidor
DROP TABLE IF EXISTS "eventos";
CREATE TABLE IF NOT EXISTS "eventos" (
	eve_id					SERIAL PRIMARY KEY,
	eve_registroID 			UUID NOT NULL,
	eve_dispositivoID		UUID NOT NULL,
	eve_timestamp			TIMESTAMP WITH TIME ZONE NOT NULL,
	eve_tipomsj				SMALLINT NOT NULL,
	eve_estadobateria 		JSONB NULL,
	eve_gpsInfo 			JSONB NULL,
    eve_gsmInfo 			JSONB NULL,
    eve_fecha 				TIMESTAMP WITH TIME ZONE NOT NULL,
    eve_giroscopio 			JSONB NULL
);
CREATE INDEX idx_eventos_tipo
    ON eventos (eve_tipomsj);

-- se almacena la descripcion de los tipos de mensajes que recibe el servidor
DROP TABLE IF EXISTS evento_descripcion;
CREATE TABLE IF NOT EXISTS "evento_descripcion" (
	evd_id SMALLINT NOT NULL PRIMARY KEY,
	evd_descripcion	VARCHAR(50) 
);

--TABLA VIEJA REEMPLAZADA POR eventos
/*
DROP TABLE IF EXISTS "reportes";
CREATE TABLE IF NOT EXISTS "reportes" (
    "id" serial NOT NULL UNIQUE,
    "key1" uuid NOT NULL, -- Referencia a ubi_registroID
    "key2" uuid NOT NULL, -- Referencia a ubi_dispositivoID
    "key3" timestamp with time zone NOT NULL, -- Referencia a ubi_timestamp
    "reporte_estadobateria" jsonb NOT NULL,
    "reporte_gpsInfo" jsonb NOT NULL,
    "reporte_gms" bigint NOT NULL,
    "reporte_fecha" bigint NOT NULL,
    "reporte_giroscopio" bigint NOT NULL,
    PRIMARY KEY ("id"),
    CONSTRAINT "reportes_fk_ubicacion"
    FOREIGN KEY ("key1", "key2", "key3") 
    REFERENCES "ubicacion"("ubi_registroID", "ubi_dispositivoID", "ubi_timestamp")
);
*/


-- SECCION ALTER TABLES --

ALTER TABLE "ubicacion" 
ADD CONSTRAINT "ubicacion_fk_dispositivo" 
FOREIGN KEY ("ubi_dispositivoID") REFERENCES "dispositivos"("disp_id");

ALTER TABLE "dispositivoconfig" 
ADD CONSTRAINT "dispositivoconfig_fk_disp"
FOREIGN KEY ("dispoconf_id") REFERENCES "dispositivos"("disp_id");

ALTER TABLE "celdas_celulares"
ADD CONSTRAINT fk_cell_reporte
FOREIGN KEY ("cell_reporte") REFERENCES "reporte_celda_celular"("rcel_numeroreporte");

ALTER TABLE "eventos"
ADD CONSTRAINT eventos_fk_ubicacion
FOREIGN KEY (eve_tipomsj) REFERENCES evento_descripcion (evd_id);