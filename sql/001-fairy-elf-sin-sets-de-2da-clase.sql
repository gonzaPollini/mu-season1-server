-- Divine (19), Red Spirit (24) y Sylphid Ray (31) piden Muse Elf en el Item.bmd original
-- (Elf/ME = 2), pero la inicializacion de OpenMU (DetermineClass) habilita tambien a la Fairy
-- Elf (clase 8). Season1 suma Iris (36), Seraphim (49) y Faith (50): todos desde Muse Elf.
-- Idempotente: correrlo de nuevo no hace nada.
-- Requiere reiniciar OpenMU (lee la configuracion al arrancar).
DELETE FROM config."ItemDefinitionCharacterClass" ic
USING config."ItemDefinition" i, config."CharacterClass" cc
WHERE i."Id" = ic."ItemDefinitionId"
  AND cc."Id" = ic."CharacterClassId"
  AND cc."Number" = 8
  AND i."Group" BETWEEN 7 AND 11
  AND i."Number" IN (19, 24, 31, 36, 49, 50);
