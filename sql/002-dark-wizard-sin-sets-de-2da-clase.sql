-- Grand Soul (18), Dark Soul (22) y Venom Mist (30) piden Soul Master en el Item.bmd original
-- (DW/SM = 2), pero la inicializacion de OpenMU (DetermineClass) habilita tambien al Dark
-- Wizard (clase 0). Season1 suma Eclipse (35) y Hades (52): todos desde Soul Master.
-- Idempotente: correrlo de nuevo no hace nada.
-- Requiere reiniciar OpenMU (lee la configuracion al arrancar).
DELETE FROM config."ItemDefinitionCharacterClass" ic
USING config."ItemDefinition" i, config."CharacterClass" cc
WHERE i."Id" = ic."ItemDefinitionId"
  AND cc."Id" = ic."CharacterClassId"
  AND cc."Number" = 0
  AND i."Group" BETWEEN 7 AND 11
  AND i."Number" IN (18, 22, 30, 35, 52);
