# Ludum Linguarum

Ludum Linguarum is a set of tools and recipes to extract localized content (currently just text) from a set of known 
games, which can then be turned into flash cards for use with [Anki](http://ankisrs.net/) (one of the most popular 
[spaced repetition](https://en.wikipedia.org/wiki/Spaced_repetition) software programs, with clients for all major
desktop and mobile platforms).

In other words, you can spice up your language learning regimen by drilling flash cards with text from video games!

[The list of currently supported games can be found here](http://enovales.github.io/LudumLinguarum/supported-games.html).
Currently only PC games are supported, although there's no technical reason other platforms couldn't be supported -- 
it's just simpler to work on PC versions of titles.

# Quick Start

This walkthrough covers setting up Ludum Linguarum, the Anki desktop client, importing text from a game, exporting it to
a format that Anki can recognize, and then importing it into Anki and using the flashcards to drill.

1. Download and extract the latest version of Ludum Linguarum, [from the list of releases above](https://github.com/enovales/LudumLinguarum/releases).
2. Download the [Anki desktop client](http://ankisrs.net/) and install it.
3. Ensure that you have at least [one of the supported games](http://enovales.github.io/LudumLinguarum/supported-games.html) installed.
4. Open up a command prompt window, in the directory to which you extracted Ludum Linguarum.
5. Type `LudumLinguarum list-supported-games` to find the name of the game that you want to extract.
6. Type `LudumLinguarum import --game="<game name>" --game-dir="<root directory of game>"` to import the game text. (Some games 
that only support a single language with their installed data may require you to specify the language with `--language-tag=<language tag>`.)
7. Type `LudumLinguarum export-anki --game="<game name>" --recognition-language="<first language>" 
--production-language="<second language>" --export-path="<path to export file>"`. The "recognition language" is the
[two-letter language code](https://en.wikipedia.org/wiki/List_of_ISO_639-1_codes) for the language you are more familiar with, 
and the "production language" is the language code for the one you're trying to learn. Examples are "en" for English, 
"fr" for French, "de" for German, "ja" for Japanese, and so on. (Flash cards will be generated for translating in both directions.) 
`export-path` is the output text file, which will be imported into Anki in the next step.
8. Open up the Anki desktop client. Click the **Create Deck** button at the bottom, and enter a name for the flashcard deck.
9. Click the **Import File** button at the bottom. Select the file you exported in step 7.
10. In the **Import** dialog, change *Type* to "Basic (optional reversed card)". *Deck* should be set to the deck you created 
in step 8. *Fields separated by* should be set to Tab, and the *Field Mapping* should be field 1 as *Front*, field 2 as *Back*, and
field 3 as *Add Reverse*. (All of these except *Type* and *Deck* should be already correct, per the defaults.)
11. Click the **Import** button. You'll probably see a warning about duplicate entries -- this is normal.
12. Now, click on the deck name in the list, and the **Study Now** button. Now you're ready to practice! I recommend
reading the [Anki documentation](http://ankisrs.net/docs/manual.html) to find more information on how to use the Anki client.

I also recommend setting up an [AnkiWeb](http://ankiweb.net/) account, as this will let you sync your progress between
the desktop and mobile clients. This makes it easy to practice on the go!

# Other notes

* If you want to focus on particular types of cards (i.e. short vocabulary words, instead of translating dialogue), don't hesitate
to use Anki's "bury" or "delete" functions -- "bury" will send the card to the bottom of the deck, while "delete" will completely
remove it. Also, depending on whether a game's extraction recipe breaks it down this way, you may be able to filter on extraction 
by "lesson" name -- see the command-line help for more details about this.

* Depending on the game, sometimes the extraction isn't 100% clean. Please let me know if you run into problems -- and in the
meantime, you can always delete incorrect cards from your decks so you can continue working with them in the meantime.

# Does this really work?

**Yes!** Lots of free and commercial software and services are based around these ideas, including Duolingo, Course Hero, Memrise,
and SuperMemo. I would strongly encourage you to try it out if you are skeptical -- you'll find it starts to improve your
recall almost immediately, and daily practice will build up your vocabulary quite quickly.