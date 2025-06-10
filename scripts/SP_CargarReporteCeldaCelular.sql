-- Stored Procedured para la insersion de filas en la tabla celdas_celulares
-- 
/*

SELECT * FROM celdas_celulares
ORDER BY cell_timestamp DESC

   CALL cargar_ReporteCeldaCelular(
       12345, -- cell_id
	   2,	  -- numeroEvento	
       310,   -- cell_mcc
       260,   -- cell_mnc
       123,   -- cell_lac
       1,     -- cell_tecnologia
       'LTE', -- cell_band
       100,   -- cell_chanel
       -85,   -- cell_nivelsenial
       '2023-01-01 12:00:00+00', -- cell_timestamp
       '550e8400-e29b-41d4-a716-446655440003', -- numeroreporte
       -122.4194, -- cell_longitud
       37.7749,   -- cell_latitud
       '550e8400-e29b-41d4-a716-446655440000', -- idRegistro
       '550e8400-e29b-41d4-a716-446655440001', -- idDispositivo
       '550e8400-e29b-41d4-a716-446655440002'  -- idAgente
   );

DROP PROCEDURE cargar_ReporteCeldaCelular(
	"cell_id" bigint, -- id de la celda
	"cell_mcc" bigint,
	"cell_mnc" bigint,
	"cell_lac" bigint,
	"cell_tecnologia" bigint,
	"cell_band" varchar(128),
	"cell_chanel" bigint,
	"cell_nivelsenial" bigint,
	"cell_timestamp" timestamp with time zone,
	"numeroreporte"	uuid, -- agrupa filas de celdas por paquete de mediciones reportado
	"cell_longitud" DOUBLE PRECISION,
	"cell_latitud" DOUBLE PRECISION,
	"id_registro" UUID,  
	"id_dispositivo" UUID,
	"id_agente" UUID,
	"numero_evento" bigint
)
*/

CREATE OR REPLACE PROCEDURE cargar_ReporteCeldaCelular(
	"cell_id" bigint, -- id de la celda
	"numero_evento" bigint,
	"cell_mcc" bigint,
	"cell_mnc" bigint,
	"cell_lac" bigint,
	"cell_tecnologia" bigint,
	"cell_band" varchar(128),
	"cell_chanel" bigint,
	"cell_nivelsenial" bigint,
	"cell_timestamp" timestamp with time zone,
	"numeroreporte"	uuid, -- agrupa filas de celdas por paquete de mediciones reportado
	"cell_longitud" DOUBLE PRECISION,
	"cell_latitud" DOUBLE PRECISION,
	"id_registro" UUID,  
	"id_dispositivo" UUID,
	"id_agente" UUID
)
LANGUAGE plpgsql AS $$


DECLARE
    resultado_hash VARCHAR(255);
    coordenadas_geom GEOMETRY(POINT, 4326);

BEGIN
    -- Calculo del hash del campo ubi_hashusuario
	-- TODO: falta usar este dato para linkear con una sesion de dispositivo al consultar en desde aplicacion
    resultado_hash := encode(digest(id_registro::TEXT || id_dispositivo::TEXT || id_agente::TEXT, 'sha256'), 'hex');

    -- Creación del objeto geometry con SRID 4326 (sistema GPS)
	IF cell_latitud IS NOT NULL AND cell_longitud IS NOT NULL
	THEN
    	coordenadas_geom := ST_SetSRID(ST_MakePoint(cell_longitud, cell_latitud), 4326);
	ELSE
		coordenadas_geom := NULL;
	END IF;
	
    -- Inserción de los valores en la tabla
	INSERT INTO celdas_celulares (
		cel_id,
		cell_nroevento,
		cel_mcc,
		cel_mnc,
		cel_lac,
		cel_tecnologia,
		cel_band,
		cell_chanel,
		cell_nivelsenial,
		cell_timestamp,
		cell_reporte, -- agrupa filas de celdas por paquete de mediciones reportado
		cell_coordenadas --SRID GPS
	) VALUES (
		cell_id,
		numero_evento,
		cell_mcc,
		cell_mnc,
		cell_lac,
		cell_tecnologia,
		cell_band,
		cell_chanel,
		cell_nivelsenial,
		cell_timestamp,
		numeroreporte,
		coordenadas_geom
	);
END;
$$;


