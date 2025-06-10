-- Funcion encargada de grabar el evento de recepcion de un mensaje de dispositivo en el servidor
/*

DROP FUNCTION grabar_evento(
	id_registro 				UUID ,
	id_dispositivo 				UUID,
	fecha_registro_evento		TIMESTAMP WITH TIME ZONE,
	tipo_mensaje				SMALLINT,
	estado_bateria				JSONB,
	gpsInfo						JSONB,
	gsmInfo						JSONB,
	fecha_mensaje				TIMESTAMP WITH TIME ZONE,
	giroscopioInfo				JSONB
);

*/


CREATE OR REPLACE FUNCTION grabar_evento(
	id_registro 				UUID ,
	id_dispositivo 				UUID,
	tipo_mensaje				SMALLINT,
	estado_bateria				JSONB,
	gpsInfo						JSONB,
	gsmInfo						JSONB,
	fecha_mensaje				TIMESTAMP WITH TIME ZONE,
	giroscopioInfo				JSONB
)
RETURNS TABLE (
	id_evento BIGINT
)

AS $$

BEGIN
	RETURN QUERY
	INSERT INTO "eventos" (
		eve_registroID,
		eve_dispositivoID,
		eve_timestamp,
		eve_tipomsj,
		eve_estadobateria,
		eve_gpsInfo,
	    eve_gsmInfo,
	    eve_fecha,
	    eve_giroscopio
	)
	VALUES (
		id_registro,
		id_dispositivo,
		now(),
		tipo_mensaje,
		estado_bateria,
		gpsInfo,
		gsmInfo,
		fecha_mensaje,
		giroscopioInfo
	)
	RETURNING eve_id;
END;	
$$ LANGUAGE plpgsql;


	