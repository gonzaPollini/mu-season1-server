// QA de requisitos de clase de sets: por personaje, /item de la armadura y mover al slot 3; registra ItemMoved vs rechazo.
// Uso: bun server/qa/sets.ts   (QA_HOST = IP LAN de OpenMU)
import { QaBot, SLOT } from './lib/bot';
import { DropItemRequestPacket } from '../../client/versions/shared/packets/ClientToServerPackets';

type Ch = { account: string; name: string; tier: 1 | 2; sets: number[]; control: number };
const E1 = [19, 24, 31, 36, 49, 50], W1 = [18, 22, 30, 35, 52], K1 = [17, 21, 29, 34, 45, 46];
const CHARS: Ch[] = [
  { account: 'qasets', name: 'qsFairy', tier: 1, sets: E1, control: 10 },
  { account: 'qasets', name: 'qsMuse', tier: 2, sets: E1, control: 10 },
  { account: 'qasets', name: 'qsDw', tier: 1, sets: W1, control: 2 },
  { account: 'qasets', name: 'qsSm', tier: 2, sets: W1, control: 2 },
  { account: 'qasets', name: 'qsDk', tier: 1, sets: K1, control: 0 },
  { account: 'qasets2', name: 'qsBk', tier: 2, sets: K1, control: 0 },
];
const FULL = [31, 35, 46]; // set completo (5 piezas) por clase
const PIECES: [string, number, number][] = [['casco', 7, SLOT.helm], ['armadura', 8, SLOT.armor], ['pantalon', 9, SLOT.pants], ['guantes', 10, SLOT.gloves], ['botas', 11, SLOT.boots]];
type Row = { char: string; set: string; pieza: string; esperado: string; obtenido: string; ok: boolean };
const rows: Row[] = [];

async function drop(bot: QaBot, slots: number[]) {
  for (const s of slots) {
    const p = DropItemRequestPacket.createPacket();
    p.TargetX = bot.info.x; p.TargetY = bot.info.y; p.ItemSlot = s;
    bot.conn.send(p.buffer); await bot.settle(120);
  }
}

/** Da, intenta vestir y deja todo limpio. Devuelve 'acepto' | 'rechazo' | 'sin-item'. */
async function tryPiece(bot: QaBot, g: number, n: number, slot: number): Promise<string> {
  const from = await bot.giveItem(g, n, 0);
  if (from < 0) return 'sin-item';
  const data = bot.inventory.get(from)!;
  const r = await bot.equip(from, slot);
  if (r.ok) {
    bot.inventory.set(slot, data);
    const back = await bot.equip(slot, from);
    if (!back.ok) throw new Error(`no pude desvestir ${g}/${n}`);
    await drop(bot, [from]);
    return 'acepto';
  }
  await drop(bot, [from]);
  return 'rechazo';
}

for (const c of CHARS) {
  const bot = await QaBot.login({ account: c.account, password: 'qasets', character: c.name });
  await drop(bot, [...bot.inventory.keys()]); // deja el inventario y el equipo vacios
  console.log(c.name, 'GM', bot.isGm, JSON.stringify(bot.info));
  for (const n of [...c.sets, c.control]) {
    const isCtl = n === c.control;
    const exp = isCtl || c.tier === 2 ? 'acepto' : 'rechazo';
    const got = await tryPiece(bot, 8, n, SLOT.armor);
    rows.push({ char: c.name, set: `${n}${isCtl ? ' (control)' : ''}`, pieza: 'armadura', esperado: exp, obtenido: got, ok: got === exp });
    console.log(c.name, 8, n, exp, got);
  }
  for (const n of FULL.filter(x => c.sets.includes(x))) {
    for (const [pn, g, slot] of PIECES) {
      if (g === 8) continue;
      const exp = c.tier === 2 ? 'acepto' : 'rechazo';
      const got = await tryPiece(bot, g, n, slot);
      rows.push({ char: c.name, set: String(n), pieza: pn, esperado: exp, obtenido: got, ok: got === exp });
      console.log(c.name, g, n, exp, got);
    }
  }
  await bot.close();
}
const ok = rows.filter(r => r.ok).length;
const md = [`# Sets: requisitos de clase (2026-10-15)`, ``, `Cuentas qasets/qasets2 (la 6ta clase va en qasets2: maximo 5 personajes por cuenta). Total OK ${ok} / FALLA ${rows.length - ok} de ${rows.length}.`, ``,
  `| personaje | set | pieza | esperado | obtenido | resultado |`, `|---|---|---|---|---|---|`,
  ...rows.map(r => `| ${r.char} | ${r.set} | ${r.pieza} | ${r.esperado} | ${r.obtenido} | ${r.ok ? 'OK' : 'FALLA'} |`)].join('\n');
await Bun.write(`${process.env.HOME}/Claude/mu-web/qa/sets/resultado-2026-10-15.md`, md + '\n');
console.log(`OK ${ok} FALLA ${rows.length - ok}`);
process.exit(0);
