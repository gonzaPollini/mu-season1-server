-- Season1: las joyas se empaquetan y desempaquetan con clic derecho en el inventario
-- (plugin JewelBundleAnywhere), asi que Lahap (NPC 256) sale de los mapas: Devias, Crywolf y Elvenland.
-- Idempotente: correrlo de nuevo no hace nada.
-- Requiere reiniciar OpenMU (lee la configuracion al arrancar).
DELETE FROM config."MonsterSpawnArea" s
USING config."MonsterDefinition" m
WHERE m."Id" = s."MonsterDefinitionId"
  AND m."Number" = 256;
