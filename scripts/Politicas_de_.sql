-- SP para la insercion de filas en la tabla ubicaciones

/*
					
CALL cargar_ubicacion(
    '550e8400-e29b-41d4-a716-446655440000',
    '550e8400-e29b-41d4-a716-446655440001',
    ST_Point(-58.3648600263424, -34.635684), -- LA BOMBONERA
    '2012-12-12 12:12:12-03',
    '550e8400-e29b-41d4-a716-446655440002'
);

SELECT * FROM ubicacion

*/


CREATE OR REPLACE PROCEDURE cargar_ubicacion(	idRegistro UUID,
    										  	idDispositivo UUID,
    											coord POINT,
    											ubi_timestamp TIMESTAMP WITH TIME ZONE,
											    agenteID UUID
												) 

LANGUAGE plpgsql AS $$

DECLARE 
resultado_hash VARCHAR(255);
coordenadas_geom GEOMETRY;

BEGIN

-- calculo el hash del campo ubi_hashusuario
resultado_hash := encode(digest(idRegistro::TEXT || idDispositivo::TEXT || agenteID, 'sha256'), 'hex');

-- se convierten las coordenadas en lat long al sistema de gps con el srid correcto
--paso mucho muy importante
coordenadas_geom := ST_SetSRID(coord::GEOMETRY, 4326);

-- incerto los valores en la tabla 

    INSERT INTO ubicacion (
        "ubi_registroID", 
        "ubi_dispositivoID", 
        ubi_coordenadas, 
        ubi_timestamp, 
        ubi_hashusuario
		) VALUES (
        idRegistro, 
        idDispositivo, 
        coordenadas_geom,
        ubi_timestamp, 
        resultado_hash
    );
END;
$$;