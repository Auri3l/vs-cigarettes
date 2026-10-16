# Cigarettes: design plan

Mod id `cigarettes`, targets Vintage Story 1.22 (.NET 10). Code mod with JSON assets.

## Tobacco

Three varieties, each with its own seeds, crop, leaves and shredded tobacco:

| Variety | Nutrient | Growth | Frost below | Heat above | Character |
|---|---|---|---|---|---|
| Virginia | K | 1.6 months | 2 °C | 36 °C | Bright all-rounder, base of most blends |
| Burley | N | 1.8 months | -1 °C | 32 °C | Hardy, cooler climates, strong in a pipe |
| Oriental | P | 1.4 months | 5 °C | 42 °C | Small aromatic leaves, needs heat |

```
seeds-<variety> ──farmland──▶ crop-<variety>-1…6 ──harvest──▶ tobaccoleaf-<variety>-fresh (+ seeds)
fresh leaf ──Dry, 96 h (6× faster on a drying rack)──▶ tobaccoleaf-<variety>-dried
dried leaf + knife ──▶ shreddedtobacco-<variety> ×2
```

## Herbs

| Herb | Nutrient | Growth | Frost below | Heat above | Becomes |
|---|---|---|---|---|---|
| Mint | N | 1.0 months | -3 °C | 32 °C | dried mint + knife → menthol crystals ×2 |
| Clove shrub | P | 2.0 months | 8 °C | 44 °C | dried cloves, used whole |

Both grow on the same crop block as tobacco, with seeds and a 48 h drying step.

## Furniture (batch processing)

All batch furniture works the same way: right click with ingredients to load them, hold right click with an empty hand to work the whole batch, and sneak + right click with an empty hand to take the ingredients back. Results go straight into your inventory.

| Block | Recipe | Batch |
|---|---|---|
| Drying rack | 6 sticks + rope | Hangs 4 leaves, drying 6× faster |
| Drying frame | 6 sticks + 3 rope | Hangs 12 leaves on three bars, drying 6× faster |
| Tobacco cutter | knife + iron ingot + 2 planks | Up to 32 dried leaves of one variety per load, a 3 s crank gives 2 shredded per leaf |
| Rolling table | 3 planks + rolling paper | Shredded tobacco + papers (+ menthol crystals or dried cloves). A 2.5 s roll makes up to 8 pairs into **3 cigarettes per paper** (hand rolling gives 2). Plain makes hand-rolled, menthol makes menthols, cloves make kreteks. |

Chopping and rolling batches count toward the Small Business, Assembly Line and Industrial Revolution achievements. Industrial Revolution grants the Industrialist trait.

## Smokables

| Item | Recipe | Puffs | Strength | Leaves behind |
|---|---|---|---|---|
| Hand-rolled cigarette | any shredded + rolling paper → 2 | 6 | 1.0 | cigarette butt |
| Hand-rolled menthol | shredded + menthol crystals + paper → 2 | 6 | 0.9 (+3% healing) | cigarette butt |
| Hand-rolled kretek | shredded + dried cloves + paper → 2 | 10 | 1.3, crackles and spits embers | cigarette butt |
| Cigar | 3 dried leaves (shapeless) | 20 | 1.5 (+ small healing bonus) | cigar butt |
| Stub cigar | 1 dried leaf + 1 shredded → 2 | 8 | 1.3 | cigar butt |
| Snipe | 6 cigarette butts + rolling paper | 4 | 0.8 | cigarette butt (the circle of life) |
| Jool temporal vaporizer | brass ingot, rusty gear, glass | 20 per tank | by e-liquid: clove 1.2, plain 1.0, menthol 0.9 | nothing, refill it |
| Wooden / bone pipe | knife + firewood / bone | 15 per bowl | by variety: Burley 1.2, Virginia 1.0, Oriental 0.9 | nothing, the pipe is reused |
| Brand cigarettes | from packs only | 8 (Pall Moll 10) | see brands | cigarette butt |

Supplies: rolling paper (knife + parchment → 8), filters (flax fibers → 4), matches (4 sticks + resin, 24 lights), Zappo lighter (brass ingot + flint, 300 lights).

**Butts.** Every smoked cigarette leaves one, and they're craftable: filter + rolling paper → 4 clean butts, filter + charcoal → 2 "pre-smoked" ones, knife + stub cigar → 2 cigar butts. The 11 filtered brands (Marlbury, Winstun, Newpart, Dunhull, Bensun, Parlement, Kant, Vague Slims, Sobrani, Mild Eleven, Chesterfeld) now take 10 cigarette butts as the filter end of their pack recipe, instead of loose filters. Six butts also recycle into a snipe.

**Flicking.** Right click with a butt to flick it. It flies off as a small entity and, where it comes to rest, turns into **ground storage**: a butt pile of up to **64 butts per block** that visibly grows as more land on it (one pile per butt type, so cigar butts pile separately). You take butts back off a pile the usual ground-storage way (1 at a time, or 8 in bulk). If it lands in an ashtray it goes in, unlocking the secret Hole in One achievement and the Deadeye trait. If it lands in water or can't settle, it just drops as an item. Sneak + right click still places butts as ground storage by hand, and right clicking an ashtray still stubs it out there. Flicking also counts toward Litterbug and Serial Litterer.

## Brands

Factory packs hold 20 and are crafted from a fixed blend. Right click a pack to shake one out; the tooltip shows the count and the brand's slogan.

| Brand | Parody of | Blend | Filter | Strength | Slogan |
|---|---|---|---|---|---|
| Marlbury | Marlboro | 4 Virginia, 4 Burley | yes | 1.1 | Come to where the flavour is. Come to Marlbury Country. |
| Camul | Camel | 5 Oriental, 3 Virginia | no | 1.0 | I'd walk a mile for a Camul. |
| Lucky Stroke | Lucky Strike | 8 Virginia, toasted over charcoal | no | 1.2 | It's toasted. Over charcoal, mostly. |
| Pall Moll | Pall Mall | 3 of each variety, longer | no | 1.0 | Outstanding! And they are long. |
| Winstun | Winston | 8 Burley | yes | 0.9 | Winstun tastes good, like a cigarette should. |
| Newpart | Newport | 4 Virginia, 4 Burley, 2 menthol | yes | 0.9 | Alive with pleasure! |
| Koal | Kool | 6 Burley, 3 menthol | no | 1.0 | Come all the way up to Koal. |
| Djarun Black | Djarum Black | 4 Virginia, 2 Oriental, 3 cloves; 12-pack, 12 puffs, crackles | no | 1.4 | Kretek Indonesia. Hear it crackle. |
| Dunhull International | Dunhill | 6 Virginia, 2 Oriental | yes | 1.0 | For people who can't afford to die cheaply. |
| Bensun & Hodges Gold | Benson & Hedges | 5 Virginia, 3 Burley, a gold nugget | yes | 1.0 | Gold-plated lungs, at last. |
| Parlement | Parliament | 4 Virginia, 4 Burley, 8 filters (recessed) | yes | 0.9 | The recessed filter keeps the tar 3 mm further away. Science! |
| Gaulwaz Brunes | Gauloises | 6 Burley, 2 Oriental, charcoal | no | 1.3 | Smoke like a philosopher. Cough like one too. |
| Kant | Kent | 4 Virginia, 4 Oriental | yes | 0.9 | Act only according to that maxim whereby you can will that everyone coughs. |
| Vague Slims | Vogue | 6 Virginia, 1 menthol; slim, 6 puffs | yes | 0.7 | Thin cigarettes. Thin excuses. |
| Sobrani Cocktail | Sobranie Cocktail | 3 of each variety, pastel papers | yes | 1.0 | Pastel colours. Charcoal lungs. |
| Belomorkanaal | Belomorkanal papirosa | 8 Burley; 25-pack, 6 puffs | no | 1.5 | Built a canal. Built a cough. |
| Mild Eleven | Mild Seven | 6 Virginia, 2 Burley | yes | 0.8 | Four more than Seven. Still not mild. |
| Chesterfeld | Chesterfield | 4 Virginia, 3 Burley, 1 Oriental | yes | 1.1 | Blow some my way. Then call a doctor. |
| Rotmans | Rothmans | 10 rot, in a **rotting pack** | no | 0.5, and it makes you hungrier | Rotmans. Matured to perfection, then a bit further. |

Each pack also shows one of 12 "Seraph General's Warnings" in red, set per brand with the `warning` attribute, for example "Smokers die younger. Seraphs respawn. Don't push it." 

Every pack recipe also takes rolling papers (usually 10), filters for filtered brands, and one to three **paint pots** in the brand's colours. Paint is a tool in the recipe: each pack uses one of a pot's 16 uses.

| Paint | Made from (plus resin and a fired bowl) |
|---|---|
| Red / Blue / Yellow | redcurrants / blueberries / honeycomb |
| Black / White / Gold | charcoal / bone meal / a native gold nugget |
| Green / Brown | fresh mint / any dried tobacco leaf |
| Pink / Purple / Teal / Orange | redcurrant + bone meal / redcurrant + blueberry / blueberry + mint / redcurrant + honeycomb |

**The rotting pack.** Rotmans is made from vanilla rot (plus brown and green paint). The pack itself perishes: about 7 days fresh, then it rots into 5 rot over 3 days. Each Rotmans cigarette goes off on its own in about 2 days. Letting a pack rot away in your inventory unlocks the secret Best Before achievement.

Brand colours: Marlbury red; Camul yellow and brown; Lucky Stroke red and black; Pall Moll, Winstun red; Newpart teal and orange; Koal green; Djarun black and gold; Dunhull red and gold; Bensun gold; Parlement, Gaulwaz, Kant, Belomorkanaal blue; Vague Slims pink; Sobrani black, pink and gold; Mild Eleven blue and white; Chesterfeld red and purple.

## Vaping

- **E-liquid** is a real liquid, so it goes in buckets, jugs and barrels. Seal 1 L of water with 4 shredded tobacco in a barrel for 24 h to get plain e-liquid. Seal plain e-liquid with 2 menthol crystals or 2 dried cloves for 12 h to flavour it.
- **Filling:** hold a container of e-liquid in the offhand and right click with the vape. The tank holds 20 puffs (0.2 L) of one flavour at a time.
- **Charging:** hold a gear in the offhand and right click. A temporal gear fills the charge to 300 puffs and a rusty gear adds 40. Each puff uses one charge. The gear is consumed.
- No lighting needed, and the exhale cloud is twice the size.

## Achievements: the Smoker's Ledger

Vintage Story has no achievement system, so the mod has its own, deliberately over-engineered one (`Smoking/AchievementSystem.cs`, defined in `config/achievements.json`).

**How it works.** Gameplay reports events (puffs, lights, packs, coughs, withdrawals, deaths). Events bump named counters and fill named sets on the player's record. An achievement unlocks when its counter reaches a target, its set reaches a size, or enough other achievements are unlocked. It only unlocks once all its prerequisites are unlocked, so the achievements form chains.

- **Tiers and points:** Bronze Lung 5, Silver Lung 15, Gold Lung 40, Platinum Lung 100 "tar points". There are 71 achievements in 7 categories, worth 1,335 points in total.
- **Ranks:** points earn ranks. Curious Seraph (0), Social Smoker (25), Pack-a-Day (75), Chain Smoker (150, gift of 8 rolling papers), Human Chimney (300), Lung Donor (500, a Golden Zappo lighter with 3,000 uses), Patient Zero (800), The Marlbury Man (1,100, 5 cigars). Every rank-up is announced to the whole server: "Ata has been promoted to Human Chimney. Their family has been notified."
- **Secrets:** 11 achievements are hidden. They show "??? Nobody knows. Keep smoking." until their prerequisites are done, then a cryptic hint ("Become the lighter." for smoking while on fire).
- **Situations:** every puff checks what's going on around you: night, rain, temporal storm, underground, high altitude, freezing, scorching, swimming, on fire, sprinting, riding, starving, 2 HP or less, next to a trader, or next to another smoker. Each one has an achievement, and Situationally Aware asks for 8 different ones.
- **Streaks and combos:** daily smoking streaks (3/7/30 in-game days), Tasting Menu (three kinds within an in-game hour), Read the Fine Print (collect all 12 pack warnings), Pack Rat (open every brand's pack).
- **Traits:** 13 achievements grant a permanent character trait. The traits are patched into the game's own trait list (`patches/traits.json`), added to the player's extra traits, and show in the character screen.

| Trait | From | Effect |
|---|---|---|
| Smoker's Lungs | Tumour Has It (1,000 puffs) | -5% hunger, -3% walk speed. "The tumour has been named Gerald." |
| Nicotine Fingers | Ouroboros (10 chain lights) | +5% ranged accuracy |
| Creature of Habit | It's an Addiction (30-day streak) | +5% healing |
| Nicotine-Stained | Nicotine Saturation | +5% mining speed, -5% healing |
| Clean Lungs | Quitter | +3% walk speed, +3% hunger ("food tastes amazing. Unfortunately.") |
| Smokescreen | Smoke Circle | animals notice you 10% later |
| Death Wish | Cause of Death: Smoking | -1 max health |
| Stakhanovite | Hero of Soviet Labour | +10% mining speed, +5% hunger |
| Tobacco Connoisseur | Brand Ambassador | -3% hunger |
| Iron Stomach | Compost Heap (100 Rotmans puffs) | -4% hunger |
| Industrialist | Industrial Revolution (100 cutter batches) | +3% mining speed |
| Deadeye | Hole in One (flick a butt into an ashtray) | +3% ranged accuracy |
| Platinum Lung | Platinum Lung | +2 max health, -10% hunger |

- **Meta:** Achievement Achievement (10 unlocked), Overachiever (25), and the secret Platinum Lung for everything else.
- **Commands:** `/smokestats` shows the summary: rank, points, a progress bar to the next rank, streak, brands, per-category counts and life lost. `/smokestats list [category]` shows every achievement with its progress bar, missing prerequisites or hint. `/smokestats ranks` lists the ranks and rewards.

## The jokes

Every item has a tongue-in-cheek description (the cigar is "Fancy cancer", the bone pipe "Carved from bone. Fitting, really."). Packs carry warnings, withdrawal says your lungs are throwing a party and keeps sending craving lines, and above 50 nicotine you randomly cough up one of six chat lines, about once every 8 minutes. It's all in `lang/en.json`, so it's easy to punch up.

## Smoking

1. Hold the smokable, put a light in the offhand: torch, lantern, any glowing item, a lit cigarette, matches or the Zappo lighter. Matches and lighters lose one use per light.
2. The first right click lights it ("(lit)" in the name). A lit item stays lit, so you don't need the light again. Lighting one cigarette splits it off the stack.
3. Each puff takes about 1.6 s (cigar 2.2 s, pipe 2.0 s), draws smoke at the mouth while held, and exhales a cloud.
4. A pipe is packed by right clicking with shredded tobacco in the offhand.
5. Butts go in the ashtray (5 stones in a U). Right click with a butt to stub it out, with an empty hand to take one back. It shows empty, some or full, holds 12, and spills them when broken.

## Effects

- Every puff applies (or refreshes) a **10-minute** buff, scaled by strength: hunger rate -25% and mining speed +5%. The cigar also adds +5% healing.
- Each puff adds its strength to the player's nicotine. Nicotine decays by 0.5 per minute.
- At 30 or more nicotine with no active buff, a mild withdrawal sets in: hunger rate +15% and mining speed -5%, an opening message, and an occasional craving line ("You'd trade a temporal gear for one drag right now."). The next puff clears it.
- All numbers are in JSON attributes (`effects`, `strength`, `maxPuffs`, `effectDurationSec`), except the withdrawal tuning at the top of `Smoking/SmokingSystem.cs`.

## Next steps

1. **First in-game run.** Compile against a real 1.22 install and fix whatever the API disagrees with.
2. **Smoking animation.** Done: `cigarettes-smoke` (plus `-fp`) and `cigarettes-exhale`, patched onto the seraph shape in `patches/smoking-animations.json`. Ata is still reviewing the poses.
3. **Real art.** Done: original textures, 3D item models, furniture shapes and a mod icon. Hand transforms are still VS defaults.
4. **More culture.** Cigarette case (holds 10 from any brand), vape flavours from fruit (VS has berries), flue-curing near heat for a quality bonus, a lit cigarette that burns down on its own, smoking idle animation while walking, smoke visible to other players, lighting and exhale sounds.
5. **Getting seeds and packs.** Wild tobacco in worldgen, seeds and packs at traders, packs in ruin loot.

## Open questions for Ata

- Decided: withdrawal stays mild, and packs are crafted with paint.
- Should packs also show up at traders and in ruins?
