-- Dark Phoenix (17), Great Dragon (21) y Dragon Knight (29) piden Blade Knight en el Item.bmd
-- original (DK/BK = 2), pero la inicializacion de OpenMU (DetermineClass) habilita tambien al
-- Dark Knight (clase 4). Season1 suma Ashcrow (34), Titan (45) y Brave (46): todos desde
-- Blade Knight. Idempotente: correrlo de nuevo no hace nada.
-- Requiere reiniciar OpenMU (lee la configuracion al arrancar).
DELETE FROM config."ItemDefinitionCharacterClass" ic
USING config."ItemDefinition" i, config."CharacterClass" cc
WHERE i."Id" = ic."ItemDefinitionId"
  AND cc."Id" = ic."CharacterClassId"
  AND cc."Number" = 4
  AND i."Group" BETWEEN 7 AND 11
  AND i."Number" IN (17, 21, 29, 34, 45, 46);
