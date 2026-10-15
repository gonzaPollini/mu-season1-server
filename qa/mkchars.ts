// Crea los personajes de QA de sets por protocolo (base class); evolucion/nivel/stats se ajustan por SQL.
import { QaBot } from './lib/bot';
import { CharacterClassNumber as C } from '../../client/src/core/types';
const list: [string, string, number][] = [
  ['qasets2', 'qsBk', C.DarkKnight],
  ['qasets', 'qsSm', C.DarkWizard], ['qasets', 'qsDk', C.DarkKnight], ['qasets2', 'qsBk', C.DarkKnight],
];
for (const [account, character, characterClass] of list) {
  try {
    const b = await QaBot.login({ account, password: 'qasets', character, createIfMissing: true, characterClass });
    console.log('ok', character); await b.close();
  } catch (e) { console.log('ERR', character, String(e)); }
}
process.exit(0);
