// Bot de QA: protocolo real de OpenMU reutilizando client/marketplace/probe (connection/session/wire).
// API: QaBot.login -> say/gm -> createMonster -> castTargeted/castArea/castSelf -> equip -> snapshot.
// Todo lo que el server manda queda en `log` con timestamp, para que el caso de prueba lo inspeccione.
import { BotConnection, type Frame } from '../../../client/marketplace/probe/connection';
import { BotSession, view } from '../../../client/marketplace/probe/session';
import {
  AreaSkillHitPacket,
  AreaSkillPacket,
  ItemMoveRequestPacket,
  PickupItemRequestPacket,
  TargetedSkillPacket,
} from '../../../client/versions/shared/packets/ClientToServerPackets';
import {
  AddNpcsToScopePacket,
  AddSummonedMonstersToScopePacket,
  CharacterInformationExtendedPacket,
  CharacterInformationPacket,
  CharacterInventoryPacket,
  ItemAddedToInventoryPacket,
  ItemMovedPacket,
  SkillAnimationPacket,
  AreaSkillAnimationPacket,
  GameServerEnteredPacket,
} from '../../../client/versions/shared/packets/ServerToClientPackets';
import { CharacterClassNumber } from '../../../client/src/core/types';

export const HOST = process.env.QA_HOST ?? '192.168.0.253';
export const GAME_PORT = Number(process.env.GAME_PORT ?? 55901);

/** Ranuras de equipo (OpenMU / MU): 0 mano izq, 1 mano der, 2 casco, 3 armadura, 4 pantalon, 5 guantes, 6 botas, 7 alas, 8 mascota, 9 colgante, 10-11 anillos. */
export const SLOT = { leftHand: 0, rightHand: 1, helm: 2, armor: 3, pants: 4, gloves: 5, boots: 6, wings: 7, pet: 8, pendant: 9, ring1: 10, ring2: 11 } as const;

export type Logged = { t: number; code: number; sub: number; len: number; bytes: Uint8Array };
export type NetObject = { id: number; type: number; x: number; y: number; summon?: boolean; seenAt: number };
export type Hit = { targetId: number; damage: number; shield: number; kind: number };
export type Effect = { active: boolean; playerId: number; effectId: number };
export type Vitals = { hp: number; maxHp: number; mana: number; maxMana: number; ag: number; maxAg: number; sd: number };

const hex = (n: number) => '0x' + n.toString(16).padStart(2, '0');
const sleep = (ms: number) => new Promise<void>(r => setTimeout(r, ms));

export type LoginOpts = {
  account: string;
  password: string;
  character?: string;
  createIfMissing?: boolean;
  characterClass?: CharacterClassNumber;
  verbose?: boolean;
};

export class QaBot {
  readonly log: Logged[] = [];
  readonly scope = new Map<number, NetObject>();
  readonly inventory = new Map<number, Uint8Array>();
  readonly messages: string[] = [];
  heroId = -1;
  vitals: Vitals = { hp: 0, maxHp: 0, mana: 0, maxMana: 0, ag: 0, maxAg: 0, sd: 0 };
  info: Record<string, number> = {};
  private waiters: { pred: (f: Logged) => boolean; res: (f: Logged) => void; timer: ReturnType<typeof setTimeout> }[] = [];
  private t0 = Date.now();

  private constructor(readonly conn: BotConnection, readonly session: BotSession, readonly opts: LoginOpts) {}

  /** Conecta, loguea y entra al mundo con el personaje pedido. */
  static async login(opts: LoginOpts): Promise<QaBot> {
    const say = (m: string) => opts.verbose && console.log(`[net] ${m}`);
    const conn = new BotConnection(HOST, GAME_PORT, say);
    const session = new BotSession(conn, opts, say);
    const bot = new QaBot(conn, session, opts);
    conn.on(f => bot.onFrame(f));
    await conn.connect();
    await session.enterWorld();
    await bot.settle(1500);
    return bot;
  }

  get name(): string { return this.opts.character ?? this.opts.account; }
  get isGm(): boolean { return this.session.gameMaster; }
  now(): number { return Date.now() - this.t0; }

  // ---- recepcion ------------------------------------------------------------

  private onFrame(f: Frame): void {
    const entry: Logged = { t: this.now(), code: f.code, sub: f.sub, len: f.bytes.length, bytes: f.bytes };
    this.log.push(entry);
    try { this.digest(entry); } catch (e) { if (this.opts.verbose) console.log(`[digest ${hex(f.code)}] ${e}`); }
    for (const w of [...this.waiters]) {
      if (w.pred(entry)) { clearTimeout(w.timer); this.waiters.splice(this.waiters.indexOf(w), 1); w.res(entry); }
    }
  }

  private digest(e: Logged): void {
    const v = view({ code: e.code, sub: e.sub, bytes: e.bytes });
    switch (e.code) {
      case 0xf1:
        if (e.sub === 0x00 && this.heroId < 0) this.heroId = new GameServerEnteredPacket(v).PlayerId;
        break;
      case 0x13: {
        const p = new AddNpcsToScopePacket(v);
        for (const n of p.getNPCs()) this.scope.set(n.Id & 0x7fff, { id: n.Id & 0x7fff, type: n.TypeNumber, x: n.CurrentPositionX, y: n.CurrentPositionY, seenAt: this.now() });
        break;
      }
      case 0x1f: {
        const p = new AddSummonedMonstersToScopePacket(v);
        for (const m of p.getSummonedMonsters()) this.scope.set(m.Id & 0x7fff, { id: m.Id & 0x7fff, type: m.TypeNumber, x: m.CurrentPositionX, y: m.CurrentPositionY, summon: true, seenAt: this.now() });
        break;
      }
      case 0x14: // objetos fuera de scope: [count, id(2 BE)...]
        for (let i = 0; i < e.bytes[3]; i++) this.scope.delete(v.getUint16(4 + i * 2, false) & 0x7fff);
        break;
      case 0xf3:
        if (e.sub === 0x03) this.readInfo(v, e.len);
        if (e.sub === 0x10) {
          this.inventory.clear();
          const p = new CharacterInventoryPacket(v);
          for (const it of p.getItems(p.ItemCount)) this.inventory.set(it.ItemSlot, new Uint8Array(it.ItemData.buffer));
        }
        break;
      case 0x22: {
        const p = new ItemAddedToInventoryPacket(v);
        this.inventory.set(p.InventorySlot, new Uint8Array(p.ItemData.buffer));
        break;
      }
      case 0x26: // vida/SD actual: extendido (24 bytes, sub 0xff) o clasico
        if (e.sub === 0xff && e.len >= 24) {
          this.vitals.hp = v.getUint32(4, true); this.vitals.sd = v.getUint32(8, true);
          this.vitals.mana = v.getUint32(12, true); this.vitals.ag = v.getUint32(16, true);
        } else if (e.sub === 0xff) { this.vitals.hp = v.getUint16(4, false); this.vitals.sd = v.getUint16(7, false); }
        break;
      case 0x27:
        if (e.sub === 0xff) { this.vitals.mana = v.getUint16(4, false); this.vitals.ag = v.getUint16(6, false); }
        break;
      case 0x0d: case 0x00: {
        const txt = [...e.bytes.slice(4)].map(b => (b >= 32 && b < 127 ? String.fromCharCode(b) : '')).join('');
        if (txt) this.messages.push(txt);
        break;
      }
    }
  }

  private readInfo(v: DataView, len: number): void {
    // Extendido: vida/mana de 32 bits. Se prueba por longitud contra la clase del cliente.
    const ext = len === CharacterInformationExtendedPacket.Length;
    const p = ext ? new CharacterInformationExtendedPacket(v) : new CharacterInformationPacket(v);
    this.info = { x: p.X, y: p.Y, map: p.MapId, str: p.Strength, agi: p.Agility, vit: p.Vitality, ene: p.Energy, cmd: p.Leadership, money: p.Money, levelUpPoints: p.LevelUpPoints };
    this.vitals = { hp: p.CurrentHealth, maxHp: p.MaximumHealth, mana: p.CurrentMana, maxMana: p.MaximumMana, ag: p.CurrentAbility, maxAg: p.MaximumAbility, sd: p.CurrentShield };
  }

  // ---- espera ----------------------------------------------------------------

  /** Primer frame (nuevo) que cumple `pred`, o null al vencer el plazo. */
  waitFor(pred: (f: Logged) => boolean, timeoutMs = 3000): Promise<Logged | null> {
    return new Promise(res => {
      const w = { pred, res: res as (f: Logged) => void, timer: setTimeout(() => { this.waiters.splice(this.waiters.indexOf(w), 1); res(null); }, timeoutMs) };
      this.waiters.push(w);
    });
  }

  /** Espera `ms` y devuelve los frames llegados en ese lapso. */
  async collect(ms: number): Promise<Logged[]> {
    const from = this.log.length;
    await sleep(ms);
    return this.log.slice(from);
  }

  async settle(ms = 500): Promise<void> { await sleep(ms); }

  // ---- chat / GM -------------------------------------------------------------

  /** Manda un comando GM (o chat) y devuelve los mensajes de texto que contesto el server. */
  async gm(cmd: string, waitMs = 700): Promise<string[]> {
    const m0 = this.messages.length;
    this.session.say(cmd);
    await sleep(waitMs);
    return this.messages.slice(m0);
  }

  /** `/createmonster <numero>`: lo crea junto al heroe, quieto. Devuelve el id de red (del mensaje o del scope). */
  async createMonster(monsterNumber: number): Promise<NetObject> {
    const before = new Set(this.scope.keys());
    const msgs = await this.gm(`/createmonster ${monsterNumber}`, 1200);
    const named = msgs.join(' ').match(/(\d+)\D*$/);
    const fresh = [...this.scope.values()].find(o => !before.has(o.id) && o.type === monsterNumber);
    const id = fresh?.id ?? (named ? Number(named[1]) : -1);
    const obj = this.scope.get(id) ?? fresh;
    if (!obj) throw new Error(`/createmonster ${monsterNumber}: no aparecio en scope (msgs: ${JSON.stringify(msgs)})`);
    return obj;
  }

  /** `/item <grupo> <numero> [nivel] ...`: OpenMU lo deja en el suelo (0x20); se levanta con 0x22. Devuelve el slot de inventario, o -1. */
  async giveItem(group: number, number: number, level = 0, extra = ''): Promise<number> {
    const dropped = this.waitFor(f => f.code === 0x20, 2500);
    await this.gm(`/item ${group} ${number} ${level} ${extra}`.trim(), 150);
    const d = await dropped;
    if (!d || d.bytes.length < 7) return -1;
    const id = ((d.bytes[5] << 8) | d.bytes[6]) & 0x7fff;
    const added = this.waitFor(f => f.code === 0x22, 2500);
    const p = PickupItemRequestPacket.createPacket();
    p.ItemId = id;
    this.conn.send(p.buffer);
    const f = await added;
    return f ? new ItemAddedToInventoryPacket(view({ code: f.code, sub: f.sub, bytes: f.bytes })).InventorySlot : -1;
  }

  // ---- skills ----------------------------------------------------------------

  /** Cast dirigido (0x19). Reune las respuestas durante `waitMs`. */
  async castTargeted(skillId: number, targetId: number, waitMs = 1500): Promise<CastResult> {
    const p = TargetedSkillPacket.createPacket();
    p.SkillId = skillId; p.TargetId = targetId;
    return this.runCast(skillId, waitMs, () => this.conn.send(p.buffer));
  }

  /** Buff/cura sobre uno mismo: skill dirigido con el id del propio heroe. */
  async castSelf(skillId: number, waitMs = 1500): Promise<CastResult> {
    if (this.heroId < 0) throw new Error('heroId desconocido (no llego GameServerEntered)');
    return this.castTargeted(skillId, this.heroId, waitMs);
  }

  /** Cast de area (0x1E) en (x,y); con `hitTargets` manda ademas 0xDB (skills de golpes explicitos). */
  async castArea(skillId: number, x: number, y: number, hitTargets: number[] = [], waitMs = 1500): Promise<CastResult> {
    const p = AreaSkillPacket.createPacket();
    p.SkillId = skillId; p.TargetX = x; p.TargetY = y; p.Rotation = 0; p.ExtraTargetId = 0xffff;
    return this.runCast(skillId, waitMs, () => {
      this.conn.send(p.buffer);
      if (hitTargets.length) {
        const h = AreaSkillHitPacket.createPacket(9 + hitTargets.length * 3);
        h.SkillId = skillId; h.TargetX = x; h.TargetY = y; h.HitCounter = 1; h.TargetCount = hitTargets.length;
        hitTargets.forEach((id, i) => { h.buffer.setUint16(9 + i * 3, id, false); h.buffer.setUint8(11 + i * 3, 1); });
        this.conn.send(h.buffer);
      }
    });
  }

  private async runCast(skillId: number, waitMs: number, send: () => void): Promise<CastResult> {
    const before = { ...this.vitals };
    const from = this.log.length;
    const t = this.now();
    send();
    await sleep(waitMs);
    const frames = this.log.slice(from);
    return summarize(skillId, before, this.vitals, frames, t, this.messages);
  }

  // ---- equipo / stats --------------------------------------------------------

  /** Mueve un item del inventario (slot) a una ranura de equipo. `ok` si el server contesta ItemMoved a esa ranura. */
  async equip(fromSlot: number, toSlot: number): Promise<{ ok: boolean; frame: Logged | null }> {
    const p = ItemMoveRequestPacket.createPacket();
    p.FromStorage = 0; p.FromSlot = fromSlot; p.ToStorage = 0; p.ToSlot = toSlot;
    p.setItemData(this.inventory.get(fromSlot) ?? new Uint8Array(12), 12);
    const answer = this.waitFor(f => f.code === 0x24, 3000);
    this.conn.send(p.buffer);
    const f = await answer;
    if (!f) return { ok: false, frame: null };
    if (f.sub === 0xff) return { ok: false, frame: f };
    const m = new ItemMovedPacket(view({ code: f.code, sub: f.sub, bytes: f.bytes }));
    return { ok: m.TargetSlot === toSlot, frame: f };
  }

  /** Da un item (`/item`) y lo viste en `slot`. */
  async giveAndEquip(group: number, number: number, level: number, slot: number, extra = ''): Promise<{ ok: boolean; from: number }> {
    const from = await this.giveItem(group, number, level, extra);
    if (from < 0) return { ok: false, from };
    const r = await this.equip(from, slot);
    return { ok: r.ok, from };
  }

  /** Stats actuales conocidos (base + vida/mana/AG) y refresco opcional. */
  snapshot(): { heroId: number; info: Record<string, number>; vitals: Vitals; scope: number } {
    return { heroId: this.heroId, info: { ...this.info }, vitals: { ...this.vitals }, scope: this.scope.size };
  }

  /** Frames del log (desde `from`) con codigo dado. */
  framesOf(code: number, from = 0): Logged[] { return this.log.slice(from).filter(f => f.code === code); }

  async close(): Promise<void> {
    try { this.session.logOut(); } catch { /* ya cerrado */ }
    await sleep(500);
    this.conn.close();
  }
}

export type CastResult = {
  skillId: number;
  accepted: boolean;
  animation: { playerId: number; targetId?: number } | null;
  effects: Effect[];
  hits: Hit[];
  killed: number[];
  mana: { before: number; after: number };
  ag: { before: number; after: number };
  hp: { before: number; after: number };
  texts: string[];
  frames: { t: number; code: string; sub: number; len: number }[];
};

function summarize(skillId: number, before: Vitals, after: Vitals, frames: Logged[], t0: number, allMsgs: string[]): CastResult {
  const effects: Effect[] = [];
  const hits: Hit[] = [];
  const killed: number[] = [];
  let animation: CastResult['animation'] = null;
  for (const f of frames) {
    const v = view({ code: f.code, sub: f.sub, bytes: f.bytes });
    if (f.code === 0x07) effects.push({ active: (f.bytes[3] & 1) === 1, playerId: v.getUint16(4, false), effectId: f.bytes[6] });
    else if (f.code === 0x19 && f.len === 9) { const a = new SkillAnimationPacket(v); animation = { playerId: a.PlayerId, targetId: a.TargetId }; }
    else if (f.code === 0x1e) { const a = new AreaSkillAnimationPacket(v); animation = { playerId: a.PlayerId }; }
    else if (f.code === 0x11) {
      if (f.len === 16) hits.push({ targetId: v.getUint16(4, true) & 0x7fff, damage: v.getUint32(8, true), shield: v.getUint32(12, true), kind: f.bytes[3] & 0x0f });
      else hits.push({ targetId: v.getUint16(3, false) & 0x7fff, damage: v.getUint16(5, false), shield: v.getUint16(8, false), kind: f.bytes[7] & 0x0f });
    } else if (f.code === 0x17) killed.push(v.getUint16(3, false) & 0x7fff);
  }
  return {
    skillId,
    accepted: animation !== null || effects.length > 0 || hits.length > 0,
    animation, effects, hits, killed,
    mana: { before: before.mana, after: after.mana },
    ag: { before: before.ag, after: after.ag },
    hp: { before: before.hp, after: after.hp },
    texts: allMsgs.slice(-3),
    frames: frames.map(f => ({ t: f.t - t0, code: hex(f.code), sub: f.sub, len: f.len })),
  };
}
