#### 1.1.1 December 7 2018
* Added support for:
	* [Super Meat Boy](https://store.steampowered.com/app/40800/) \[de, en, es, fr, it, ja, kr, pl, pt, ru, zh-CN, zh-TW\]
	* [The Escapists](https://store.steampowered.com/app/298630/) \[de, en, es, fr, it, pl, ru\]

#### 1.1.0 November 29 2018
* Added support for exporting for use with SuperMemo. The text files are tab-delimited, and split into files of 99 entries each, to work with SuperMemo's importer tool.

#### 1.0.3 October 9 2018
* Switched resource loading from assemblies to use Mono.Cecil, for compatibility.
* Fixed support for codepage 1252 and Shift-JIS for netcoreapp.

#### 1.0.2 June 30 2018
* Region names in language tags are now normalized when adding cards to the database.
* Added a new 'list-supported-languages' option, which shows all languages that are supported by at least one game.
* 'list-supported-games' now lists the languages known to be supported by each game.
* 'list-supported-games' now supports a '--languages' parameter, which allows you to filter based on particular 
   languages. This lets you easily determine which games might be appropriate for the language you're trying to learn.
* Added support for:
  * [The Witness](https://store.steampowered.com/app/210970/) \[ar, de, en, es-ES, es-LA, fr, hu, id, it, ja, ko, pl, pt-BR, pt-PT, ru, zh-CN, zh-TW\]
  * [Prison Architect](https://store.steampowered.com/app/233450/) \[bg, cs, da, de, el, en, es, fi, fr, hu, it, ja, ko, nl, no, pl, pt-PT, pt-BR, ro, ru, sv, th, tr, uk, zh-CN, zh-TW\]

#### 1.0.1 May 31 2018
* Built against .NET Core 2.1.
* Changed Windows builds to use self-contained deployments.

#### 1.0.0 May 30 2018
* Major update: Ludum Linguarum should now run on OS X and Linux!
	* Some games may not be available under non-Windows operating systems. (Currently, this only applies to Star Wars: Galactic Battlegrounds Saga.)
	* Game extraction recipes are still based on the Windows versions of games. If the resource files for the OS X/Linux version of a game are significantly different, Ludum Linguarum may fail to extract the assets.

#### 0.17.2 May 9 2018
* Fixed support for [Pillars of Eternity II: Deadfire](http://store.steampowered.com/app/560130/) \[de, en, es, fr, it, ko, pl, pt, ru, zh-CN\] to work with the final released game.

#### 0.17.0 - January 24 2018
* Added support for:
  * [Pillars of Eternity II: Deadfire](http://store.steampowered.com/app/560130/) \[de, en, es, fr, it, ko, pl, pt, ru, zh-CN\]

#### 0.16.0 - December 26 2017
* Added support for:
	* [Darwinia](http://store.steampowered.com/app/1500/) \[de, en, es, fr, it\]
	* [Defcon](http://store.steampowered.com/app/1520/) \[de, en, es, fr, it\]
	* [Multiwinia](http://store.steampowered.com/app/1530/) \[de, en, es, fr, it\]

#### 0.15.0 - December 8 2017
* Added support for:
    * [F1 Race Stars](http://store.steampowered.com/app/203680/F1_RACE_STARS/) \[de, en, es, fr, it, ja, pl, pt-BR\]
    * [F1 2011](http://store.steampowered.com/app/44360/F1_2011/) \[de, en, es, fr, it, ja, pl, pt-BR, pt-PT, ru\]
    * [F1 2012](http://store.steampowered.com/app/208500/F1_2012/) \[de, en, es, fr, it, ja, pl, pt-BR, ru\]
    * [F1 2014](http://store.steampowered.com/app/226580/F1_2014/) \[de, en, es, fr, it, ja, pl, pt-BR\]
    * [F1 2015](http://store.steampowered.com/app/286570/F1_2015/) \[de, en, es, fr, it, ja, pl, pt-BR, ru, zh-CN, zh-TW\]
    * [F1 2016](http://store.steampowered.com/app/391040/F1_2016/) \[de, en, es, fr, it, ja, pl, pt-BR, ru, zh-CN, zh-TW\]
    * [DiRT](http://store.steampowered.com/app/11440/) \[de, en-GB, en-US, es, fr, it\]
    * [DiRT 2](http://store.steampowered.com/app/12840/) \[de, en-GB, en-US, es, fr, it, ja, pl, ru\]
    * [GRID](http://store.steampowered.com/app/12750/) \[de, en-GB, en-US, es, fr, it, ja\]
    * [GRID 2](http://store.steampowered.com/app/44350/GRID_2/) \[de, en-GB, en-US, es, fr, it, ja, pl, pt-BR\]
    * [GRID Autosport](http://store.steampowered.com/app/255220/GRID_Autosport/) \[de, en-GB, en-US, es, fr, it, ja, pl, pt-BR, ru\]

#### 0.14.0 - July 17 2017
* Added command line options for 'export-anki' to limit the length (in characters or whitespace-delimited words) of the text of a card: --recognition-length-limit, --production-length-limit, --recognition-word-limit, and --production-word-limit.
* Changed to use database files per-game, instead of a global one, to make them easier to manage when many games have been extracted.
* Some text cleanups (collapsing multiple whitespace characters into a single space, trimming ends) are now automatically applied to all games.
* Fixed a bug with importing Age of Empires II: HD Edition.
* Added support for:
    * [Torment: Tides of Numenera](http://store.steampowered.com/app/272270/Torment_Tides_of_Numenera/) \[de, en, es, fr, it, pl, ru\]
	* [Tyranny](http://store.steampowered.com/app/362960/Tyranny/) \[de, en, es, fr, pl, ru\]

#### 0.13.0 - November 29 2016
* Added support for:
    * [Age of Empires III](http://store.steampowered.com/app/105450/) \[de, en, es, fr, it\]
    * [Braid](http://store.steampowered.com/app/26800/) \[cs, de, en, es, fr, it, ja, ka, ko, pl, pt, ru, zh\]
    * [Crusader Kings II](http://store.steampowered.com/app/203770/) \[de, en, es, fr\]
    * [Orcs Must Die! 2](http://store.steampowered.com/app/201790/) \[de, en, es, fr, it, ja, pl, pt, ru\]
    * [Transistor](http://store.steampowered.com/app/237930/) \[de, en, es, fr, it, ja, pl, pt, ru, zh\]
    * [IHF Handball Challenge 12](http://store.steampowered.com/app/283490/) \[da, de, en, es, hu, it, pt\]
    * [IHF Handball Challenge 14](http://store.steampowered.com/app/279460/) \[da, de, en, es, fr, pl, sv\]

#### 0.12.0 - November 28 2016
* Added support for:
    * [Sonic Adventure DX](http://store.steampowered.com/app/71250/) (partial) \[de, en, es, fr, it, ja\]
	* [Star Wars Galactic Battlegrounds Saga](http://store.steampowered.com/app/356500/) \[de, en, es, fr\]

#### 0.11.0 - July 10 2016
* A new 'dump-text' command was added, which allows extracted game text (optionally subject to filtering or 
random sampling) to be dumped to a tab-separated text file.

#### 0.10.0 - June 30 2016
* Added support for:
  * [Age of Empires II HD](http://store.steampowered.com/app/221380/) \[de, en, es, fr, it, ja, ko, nl, pt, ru, zh\]
  * [Space Channel 5: Part 2](http://store.steampowered.com/app/71260/) \[en, jp, de, es, fr, it\]
  * [Puzzle Kingdoms](http://store.steampowered.com/app/23700/) \[de, en, es, fr, it\]
  * [Sid Meier's Civilization IV](http://store.steampowered.com/app/3900/) \[de, en, es, fr, it\]
  * [Sid Meier's Civilization IV: Warlords](http://store.steampowered.com/app/3990/) \[de, en, es, fr, it\]
  * [Sid Meier's Civilization IV: Beyond the Sword](http://store.steampowered.com/app/8800/) \[de, en, es, fr, it\]
  * [Sid Meier's Civilization IV: Colonization](http://store.steampowered.com/app/16810/) \[de, en, es, fr, it\]
  * [Europa Universalis III](http://store.steampowered.com/app/25800/) \[en, de\]
  * [Europa Universalis IV](http://store.steampowered.com/app/236850/) \[de, en, es, fr\]
  * [Hearts of Iron 3](http://store.steampowered.com/app/25890/) \[de, en, fr\]
  * [Victoria 2](http://store.steampowered.com/app/42960/) \[de, en, fr\]

#### 0.9.0 - May 23 2016
* Initial version
* Supports exporting content as [Anki](http://ankisrs.net/) flashcards
* Games supported, and languages supported (for games that are a single download for all languages):
  * [Audiosurf](http://store.steampowered.com/app/12900/) \[en, ru\]
  * [Bastion](http://store.steampowered.com/app/107100/) \[de, en, es, fr, it\]
  * [Hatoful Boyfriend](http://store.steampowered.com/app/310080/) \[de, en, es, fr, it, ja, ru\]
  * [Hatoful Boyfriend: Holiday Star](http://store.steampowered.com/app/377080/) \[de, en, fr, ja\]
  * [Hell Yeah!](http://store.steampowered.com/app/205230/) \[en, de, es, fr, it, ja\]
  * [Jade Empire](http://store.steampowered.com/app/7110/) 
  * [Jet Set Radio](http://store.steampowered.com/app/205950/) \[de, en, es, fr, it, ja\]
  * [Madballs: Babo Invasion](http://store.steampowered.com/app/25700/) \[de, en, es, fr, it, ja, ko, pt, ru, zh\]
  * [Magical Drop V](http://store.steampowered.com/app/204960/) \[de, en, es, fr, it, ja\]
  * [Magicka](http://store.steampowered.com/app/42910/) \[de, en, es, fr, hu, it, pl, ru\]
  * [Neverwinter Nights](https://www.gog.com/game/neverwinter_nights_diamond_edition)
  * [Orcs Must Die!](http://store.steampowered.com/app/102600/) \[de, en, es, fr, it, ja, pl, pt, ru\]
  * [Pillars of Eternity](http://store.steampowered.com/app/291650/) \[de, en, es, fr, it, ko, pl, ru\]
  * [Puzzle Chronicles](http://store.steampowered.com/app/19020/) \[de, en, en-gb, es, es-mx, fr, fr-ca, it\]
  * [Puzzle Quest 2](http://store.steampowered.com/app/47540/) \[de, en, es, fr, it\]
  * [Skulls of the Shogun](http://store.steampowered.com/app/228960/) \[de, en, es, fr, it, ja, ko, pt, ru, zh\]
  * [Star Wars: Knights of the Old Republic](http://store.steampowered.com/app/32370)
  * [Star Wars: Knights of the Old Republic II](http://store.steampowered.com/app/208580/)
  * [The King of Fighters '98 Ultimate Match](http://store.steampowered.com/app/222420/) \[en, ja\]
  * [The King of Fighters 2002 Unlimited Match](http://store.steampowered.com/app/222440/) \[en, ja\]
  * [Worms Armageddon](http://store.steampowered.com/app/217200/) \[de, en, es, fr, it, nl, pt, ru, sv\]
