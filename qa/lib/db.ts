// Acceso a la base de OpenMU por `docker exec openmu-db psql` (no hay psql en el host).
// Solo lecturas y escrituras sobre filas de cuentas qa* (ver setup.ts).

const PSQL = ['docker', 'exec', '-i', 'openmu-db', 'psql', '-U', 'postgres', '-d', 'openmu', '-v', 'ON_ERROR_STOP=1', '-qAt'];

/** Corre SQL y devuelve la salida cruda. Lanza si psql falla. */
export function sql(text: string): string {
  const p = Bun.spawnSync(PSQL, { stdin: Buffer.from(text) });
  if (p.exitCode !== 0) throw new Error(`psql: ${p.stderr.toString().trim()}\n${text}`);
  return p.stdout.toString().trim();
}

/** Filas de un SELECT como objetos (envuelve la consulta en json_agg). */
export function rows<T = Record<string, unknown>>(select: string): T[] {
  const out = sql(`select coalesce(json_agg(t), '[]') from (${select}) t;`);
  return JSON.parse(out || '[]') as T[];
}

/** Escapa un literal de texto SQL. */
export const lit = (s: string): string => `'${s.replace(/'/g, "''")}'`;

/** Guarda filas de las tablas tocadas antes de un UPDATE (regla: respaldo previo). */
export function backup(file: string, selects: Record<string, string>): void {
  const dump: Record<string, unknown> = {};
  for (const [name, select] of Object.entries(selects)) dump[name] = rows(select);
  Bun.write(file, JSON.stringify(dump, null, 1));
}
