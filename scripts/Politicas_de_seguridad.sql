-- Script con configuraciones de permisos y seguiridad para usuarios y BDD en general

-- Configuracion de permisos en tabla ubicacion
REVOKE ALL ON ubicacion FROM PUBLIC;

GRANT SELECT ON ubicacion TO lector;
GRANT INSERT ON ubicacion TO escritor;

-- politicas de seguridad para la tabla ubicaciones
ALTER TABLE ubicacion ENABLE ROW LEVEL SECURITY;

CREATE POLICY solo_leer
ON ubicacion
FOR SELECT
USING (true);


