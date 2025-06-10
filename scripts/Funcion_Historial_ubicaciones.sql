-- funcion para crear un historial de ubicacion dado a partir de un registroid, dispisitivoid y agenteid
/*
SELECT * FROM ubicacion
"550e8400-e29b-41d4-a716-446655440000" --registroID
"550e8400-e29b-41d4-a716-446655440001" --dispositivoID
"cefdd7218105bfe525fb22879a38d38f5c97c3d3c377b6e2cf8fa6e7eef1cfa3"
"cefdd7218105bfe525fb22879a38d38f5c97c3d3c377b6e2cf8fa6e7eef1cfa3"

DO $$
declare
idRegistro UUID := '550e8400-e29b-41d4-a716-446655440000';
idDispositivo UUID := '550e8400-e29b-41d4-a716-446655440001';
idAgente UUID := '550e8400-e29b-41d4-a716-446655440002';
resultado_hash TEXT;

BEGIN
    resultado_hash := encode(digest(idRegistro::TEXT || idDispositivo::TEXT || idAgente::TEXT, 'sha256'), 'hex');
    RAISE NOTICE 'El hash resultante es: %', resultado_hash;
END $$;


select * from Ubicacion

SELECT * 
FROM crear_historial_ubicacion(
    '550e8400-e29b-41d4-a716-446655440000',
    '550e8400-e29b-41d4-a716-446655440001',
    '550e8400-e29b-41d4-a716-446655440002',
    '20-10-04 11:48:00-03',
    '2026-03-09 11:21:51'
);

*/

CREATE OR REPLACE FUNCTION crear_historial_ubicacion(
    idRegistro UUID,
    idDispositivo UUID,
    agenteID UUID,
    fecha_desde TIMESTAMP WITH TIME ZONE,
    fecha_hasta TIMESTAMP WITH TIME ZONE
)
RETURNS TABLE (
    ubi_coordenadas geometry(Point, 4326),
    ubi_timestamp   TIMESTAMP WITH TIME ZONE
) AS $$
DECLARE
    resultado_hash VARCHAR(255);
BEGIN
    -- Calcular el hash del campo ubi_hashusuario
    resultado_hash := encode(digest(idRegistro::TEXT || idDispositivo::TEXT || agenteID, 'sha256'), 'hex');

    -- Devolver el resultado del SELECT
    RETURN QUERY
    SELECT u.ubi_coordenadas, u.ubi_timestamp
    FROM ubicacion u
    WHERE u.ubi_hashusuario = resultado_hash
      AND u.ubi_timestamp BETWEEN fecha_desde AND fecha_hasta
    ORDER BY u.ubi_timestamp DESC;
END;
$$ LANGUAGE plpgsql;