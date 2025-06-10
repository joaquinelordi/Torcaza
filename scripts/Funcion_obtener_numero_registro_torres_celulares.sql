-- funcion para la creacion de un numero de reporte de manera centralizada
-- recibe los campos id registro, id dispositivo y id agente para llevar un registro de a quien pertenece ese numero de reporte
-- devuelve el numero de reporte creado

/*

SELECT * FROM reporte_celda_celular

DROP FUNCTION obtener_numero_registro_torres_celulares(
	idRegistro UUID,
	idDispositivo UUID,
	idAgente UUID
);

*/

CREATE FUNCTION obtener_numero_registro_torres_celulares(
	id_registro UUID ,
	id_dispositivo UUID,
	id_agente UUID
)
RETURNS TABLE (
	numero_reporte UUID
)

AS $$
DECLARE	
    numero_reporte UUID := uuid_generate_v4();
BEGIN
	INSERT INTO reporte_celda_celular (
		rcel_numeroreporte,
        rcel_idregistro,
        rcel_iddispositivo,
        rcel_agenteid,
		rcel_fecha
	)
	VALUES (
		numero_reporte,
		id_registro,
		id_dispositivo,
		id_agente,
		now()
	);
	
	RETURN QUERY
	SELECT numero_reporte;
END;
$$ LANGUAGE plpgsql;

