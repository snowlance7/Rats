# Rats Mod

![Gathering Food](https://imgur.com/aBnca3P.gif)
![Rat Chase](https://imgur.com/JXKzikz.gif)
![Rat Venting](https://imgur.com/gAWqA56.gif)

## Behavior

<details>
<summary>Spoilers</summary>

- **Rat Nest:** A sewer grate can spawn on any map as a map hazard which will spawn rats every x - x seconds.
- **Rat Types:** Each rat will either defend the nest or scout the dungeon. If a player gets too close to the nest, they will damage the player.
- **Enemy Vents:** If a scout rat cant get to a desired location, it will try to crawl through the vents to get to that location.
- **Lost Rats:** If a rat cannot get to the nest, even by vents, it will roam at random. These rats will never attack the player or enemies. They are only aggressive when part of a colony.
- **Colony Threat System:** Each time a rat sees a player or enemy, they will add 1 to a threat counter. When that reaches a threshold (100 for players and 50 for enemies by default) they will start swarming the threat.
- **Swarming:** When enough rats are swarming a target, they will begin attacking the target, dealing 2 damage each bite.
- **Rat Food:** If a rat finds a player corpse it will attempt to drag the corpse back to the nest. They will rip food off of enemies depending on the enemies HP. By default, player corpses give 30 food and enemies give `10 * enemyHP` food. For every 10 food brought back to the nest, it will spawn a rat.
- **Rat Control:** You can stop rats from spawning at a nest by finding the terminal code located on the nest. Inputing this code into the terminal will open/close the grate.

</details>

## Planned features

- Rat king
- Diseased rats that can make the player get sick, lowering stamina and making them cough, etc.
- Rallying: If a player or enemies threat level is high enough, a rat will return to the nest and rally any nearby rats to form a kill squad to hunt down their target.
- Outside rats: Chance for rat nests to spawn outside as vain shrouds. Keeping these under control will require weed killer or traps.
- More rat types?
- Better models, textures and animations.
- Rat items like cheese to lure them away, rat traps or a nailgun turret.
- Optimizations so there can be more rats with less performance issues if possible.
- More configs for performance.

## Contact

This is currently in beta! For issues or suggestions visit the [github](https://github.com/snowlance7/Rats) or [Modding Discord](https://discord.com/channels/1168655651455639582/1309390403459354715).

## For collaboration or queries

### Snowy
- Discord: [Snowy](https://discord.com/users/327989194087727107)
- GitHub: [snowlance7](https://github.com/snowlance7)
- Ko-fi: [snowlance](https://ko-fi.com/snowlance) (Any money will go towards better animations and models for my mods)

## Credit

- Dev general for the coding help as always
- [Drainage grate](https://skfb.ly/ouBVu)
- [Rat Animated](https://skfb.ly/oEq7y)