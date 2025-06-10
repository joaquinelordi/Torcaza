-- SP para la insercion de filas en la tabla ubicaciones

/*
					
CALL cargar_ubicacion(
    '550e8400-e29b-41d4-a716-446655440000',
    '550e8400-e29b-41d4-a716-446655440001',
    ST_Point(-58.3648600263424, -34.635684), -- LA BOMBONERA
    '2025-03-10 12:12:12-03',
    '550e8400-e29b-41d4-a716-446655440002'
);

SELECT * FROM ubicacion

DROP PROCEDURE cargar_ubicacion(
    idRegistro UUID,
    idDispositivo UUID,
    latitud DOUBLE PRECISION,
    longitud DOUBLE PRECISION,
    ubi_timestamp TIMESTAMP WITH TIME ZONE,
    agenteID UUID
);


*/


CREATE OR REPLACE PROCEDURE cargar_ubicacion(
    idRegistro UUID,
    idDispositivo UUID,
	numero_evento BIGINT,
    latitud DOUBLE PRECISION,
    longitud DOUBLE PRECISION,
    ubi_timestamp TIMESTAMP WITH TIME ZONE,
    agenteID UUID
)
LANGUAGE plpgsql AS $$

DECLARE 
    resultado_hash VARCHAR(255);
    coordenadas_geom GEOMETRY(POINT, 4326);

BEGIN
    -- Calculo del hash del campo ubi_hashusuario
    resultado_hash := encode(digest(idRegistro::TEXT || idDispositivo::TEXT || agenteID::TEXT, 'sha256'), 'hex');

    -- Creación del objeto geometry con SRID 4326 (sistema GPS)
    coordenadas_geom := ST_SetSRID(ST_MakePoint(longitud, latitud), 4326);

    -- Inserción de los valores en la tabla
    INSERT INTO ubicacion (
        "ubi_registroID", 
        "ubi_dispositivoID", 
		"ubi_nroevento",
        ubi_coordenadas, 
        ubi_timestamp, 
        ubi_hashusuario
    ) VALUES (
        idRegistro, 
        idDispositivo,
		numero_evento,
        coordenadas_geom,
        ubi_timestamp, 
        resultado_hash
    );
END;
$$;
