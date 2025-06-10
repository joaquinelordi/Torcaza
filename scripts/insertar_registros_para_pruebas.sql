-- creacion de registros para pequeñas pruebas
/*
"dispoconf_id" uuid NOT NULL UNIQUE,
    "dispoconf_latencia" bigint NOT NULL,
    "dispoconf_mododefault" varchar(255) NOT NULL,
    "dispoconf_habilitado" bigint NOT NULL,
    PRIMARY KEY ("dispoconf_id")
);

    '550e8400-e29b-41d4-a716-446655440000',
    '550e8400-e29b-41d4-a716-446655440001',
    ST_Point(-58.3816, -34.6037),
    '2024-11-15 15:00:00+00',
    'usuario123'

CREATE TABLE IF NOT EXISTS "dispositivos" (
    "disp_id" uuid NOT NULL UNIQUE,
    "disp_MACprimaria" varchar(255) NOT NULL UNIQUE,
    "disp_MACsecundaria" varchar(255) NOT NULL UNIQUE,
    "disp_habilitado" boolean,
    PRIMARY KEY ("disp_id")
);

ALTER TABLE ubicacion ADD COLUMN ubi_id SERIAL;

ALTER TABLE ubicacion DROP CONSTRAINT ubicacion_pkey;

ALTER TABLE reportes_completos DROP CONSTRAINT reportes_completos_fk1;

ALTER TABLE reportes DROP CONSTRAINT reportes_fk_ubicacion;

ALTER TABLE ubicacion ADD CONSTRAINT ubicacion_pkey PRIMARY KEY (ubi_id);


	SELECT conname AS constraint_name, conrelid::regclass AS table_name
FROM pg_constraint
WHERE confrelid = 'ubicacion'::regclass AND contype = 'f';

SELECT column_name, data_type, is_nullable
FROM information_schema.columns
WHERE table_name = 'ubicacion';


*/	

INSERT INTO dispositivos (
					disp_id,
					"disp_MACprimaria",
					"disp_MACsecundaria",
					disp_habilitado
				)
			VALUES (
					'550e8400-e29b-41d4-a716-446655440001',
					'0A-00-27-00-00-15',
					'00-FF-1E-DB-5F-4B',
					true
				);
				
SELECT * FROM dispositivos
				
INSERT INTO dispositivoConfig (
					"dispoconf_id",
					dispoconf_latencia,
					dispoconf_mododefault,
					dispoconf_habilitado
					)
				VALUES	(
					'550e8400-e29b-41d4-a716-446655440001',
					3600,
					'T',
					1
			);

SELECT * FROM dispositivoConfig


INSERT INTO ubicacion (
					ubi_registroID,
					"ubi_dispositivoID",
					ubi_coordenadas,
					ubi_timestamp,
					ubi_hashusuario
				) 
				VALUES	(
					'550e8400-e29b-41d4-a716-446655440000',
					'550e8400-e29b-41d4-a716-446655440001',
					ST_Point(-34.635684, -58.3648600263424),
					'2012-12-12 12:12:12-03',
					
			);

SELECT * FROM ubicacion			

			
