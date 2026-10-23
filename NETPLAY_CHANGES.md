# Network chnages

Read [Readme](README.md) first and ensure the mod is installed

This will list hopefully exhaustively what are the changes made by the mod.
Those change where made for balances purposes.

## Pause ? what is that ?

There is no pause on netplay. Die

## Synchronization

Most of the stuff are synchronized but some are per player while other are fully sychronized

For example buying something from the shady guy is locally simulated meaning he won't flee when you buy somehing for the others

Charging shrines are instead fully synchronized meaning every one will get the bonus.

> [!NOTE]  
> There is a bug where you can miss either your level up or your shrine upgrade because both can't happen at the same time. Will try to fix later

Magnet (Shrine and pickup) make the xp goes to a random player (No the one that interacted won't get all the xp)

You get i-frames when leveling up/getting a reward/openning a chest. This is to counter balance the fact that the game won't be paused anymore. 5 sec for choosing a reward and 1 second when finishing

<p align="center">
  <img src="images/timer.gif" alt="timer" />
</p>

> [!NOTE]  
> You can also still move behind when selecting a reward

## Disabled Save/Steam interaction

The game naturally save progression and update your achievement. The game is not really meant to be played online so all of the save/steam interaction is prohibited when playing a netplay game.
Same thing with uploading your score to the leaderbord, you would get banned anyway , so let's not do that 😅

### Shared Kills (netplay option)

By default every player only counts their own kills. With the **Shared Kills** option (Netplay options menu, decided by the host for the whole lobby), the kill counter in the HUD and the kills / elite kills / boss kills shown at the end of the run are cumulated across all players, so everyone sees the same team total.
Because those counters are not yours alone anymore, saving progression is forced off for everyone in the lobby while the option is on, whatever the "Allow Saving progression" setting says. Your persistent stats and achievements are never touched by the other players' kills.

### Difficulty (netplay option)

A slider in the Netplay options menu, from **0.5x** to **2.0x** in steps of 0.1 (1.0x is the balance described below). Like Shared Kills, the **host's value applies to the whole lobby**: it is sent to the matchmaking server when you connect and copied to every player, and a notification shows the lobby difficulty when the match is found. Changing it after connecting only affects your next session.

What the multiplier `d` changes, on top of the player count scaling:

- Enemy HP, enemy damage and elite chance: multiplied by `d`
- Enemy speed: multiplied by `1 + (d - 1) × 0.15` (2.0x gives +15%, 0.5x gives -7.5%; scaling speed fully would be unplayable)
- Max spawnable enemies: multiplied by `1 + (d - 1) × 0.5`, capped at 800 (2.0x gives 750 for 2-3 players, 800 for 4-6). The Final Swarm cap stays at 400
- Spawn credits: the formula includes `1 + (d - 1) × 0.05`, but the credits patch is currently disabled (FPS), so difficulty does not change the spawn rate yet

XP, gold, chest spawn rate and lamp charge time are not affected.

## Custom game balance

The mod feature some game code to try to re balance the game as more player join a session.
This is **CERTAINLY** not perfect and probably unbalanced right now and will need to be tuned as more people try and report (For any dev ,the class i'm refering is `GameBalanceService.cs`). Those are the stat being modified:

- Credit timer , The credit is a value the in game sumonner use to know how much it can spawn enemies. The more player, the more credit the summonner get

- Free Chest Spawn Rate, the more player we have in a session, the more free chest we should have on the map

- Pickup Xp : The xp you get from the pickup, since pickup are synchronized, i realized player get rapidly under level since they are sharing XP. The mod will boost a bit the reward of XP on netplay to counterbalance the fact that there are less XP. The current balance is **DOUBLE** XP for 2-4 players and **TRIPLE** XP for 5 and 6 players.

- Boss lamp charge: In the graveyard, the final boss is naturally weaker the more player with have as they can quicly charge lamp on their own. To counterbalance that, there is some additionnal time required to charge a lamp. The initial charge time when playing solo is 3, its doubled for example at 2 players and quadrupled at six players.

- Max spawnable enemies: 400 and under on a final swarm (keeping the original cap) ,500 up to 4 player and 600 for 5-6 players (untested ,you have been warned). Scaled by the Difficulty option, see above

- Enemy HP: +20% per additional **alive** player (enemies spawn weaker once someone is dead), on top of a stage multiplier (x1.1 on stage 2, x1.2 on stage 3) and a bonus for bosses, elites, minibosses and challenge enemies

- Enemy damage: +10% per additional player. Enemy speed: +3% per additional player. Elite chance: +15% per additional player. These three use the number of players connected when the stage started, so they don't drop when a teammate dies. HP, damage, speed and elite chance are then multiplied by the Difficulty option, see above
