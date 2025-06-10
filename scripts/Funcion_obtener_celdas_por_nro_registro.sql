-- funcion para obtener una lista de registros de celdas celulares a partir de un numero de registro
-- te devuelve una serie de filas que fueron obtenidas en una misma medicion, lo que se asocia al mismo numero de registro 
/* 
TODO!!!! ---> falta agragar campos de radio y una tabla para calcularlo tambien 
SELECT * 
FROM obtener_celdas_por_nro_registro(
    '550e8400-e29b-41d4-a716-446655440000',
    '550e8400-e29b-41d4-a716-446655440001',
    '550e8400-e29b-41d4-a716-446655440002',
	'ee69eef7-4493-42fe-a196-82eccc518d5c' -- numero de reporte	
);

SELECT * FROM celdas_celulares

DROP FUNCTION obtener_celdas_por_nro_registro(
	idRegistro UUID,
	idDispositivo UUID,
	idAgente UUID,
	nroReporte UUID
) 


*/


CREATE OR REPLACE FUNCTION obtener_celdas_por_nro_registro(
	idRegistro UUID,
	idDispositivo UUID,
	idAgente UUID,
	nroReporte UUID
) 
RETURNS TABLE (
	cell_id bigint,
	cell_mcc bigint,
	cell_mnc bigint,
	cell_lac bigint,
	cell_tecnologia bigint,
	cell_band varchar(128),
	cell_channel bigint,
	cell_nivelsenial bigint,
	cell_timestamp TIMESTAMP WITH TIME ZONE,
	cell_reporte UUID,
	cell_coordenadas geometry(Point, 4326)			
)

AS $$
DECLARE	
    resultado_hash VARCHAR(255);
BEGIN
    -- Calcular el hash del campo ubi_hashusuario
	--TODO: por ahora no hago nada con este resultado, despues veremos si sirve para validar
    resultado_hash := encode(digest(idRegistro::TEXT || idDispositivo::TEXT || idAgente, 'sha256'), 'hex');

    -- Devolver el resultado del SELECT
	RETURN QUERY
	SELECT  c.cel_id,
			c.cel_mcc,
			c.cel_mnc,
			c.cel_lac,
			c.cel_tecnologia,
			c.cel_band,
			c.cell_chanel,
			c.cell_nivelsenial,
			c.cell_timestamp,
			c.cell_reporte,
			c.cell_coordenadas			
	FROM celdas_celulares c
	WHERE c.cell_reporte = nroReporte AND c.cell_coordenadas IS NOT NULL
	ORDER BY c.cell_timestamp DESC;
END;
$$ LANGUAGE plpgsql;

