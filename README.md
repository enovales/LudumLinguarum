# Ludum Linguarum

[![Build status](https://ci.appveyor.com/api/projects/status/v73o4eflpx781va3/branch/master?svg=true)](https://ci.appveyor.com/project/ErikNovales/ludumlinguarum/branch/master)

Ludum Linguarum is a set of tools and recipes to extract localized content (currently just text) from a set of known 
games, which can then be turned into flash cards for use with [Anki](http://ankisrs.net/) (one of the most popular 
[spaced repetition](https://en.wikipedia.org/wiki/Spaced_repetition) software programs, with clients for all major
desktop and mobile platforms).

In other words, you can spice up your language learning regimen by drilling flash cards with text from video games!

[The list of currently supported games can be found here](http://enovales.github.io/LudumLinguarum/supported-games.html).
Currently only PC games are supported, although there's no technical reason other platforms couldn't be supported -- 
it's just simpler to work on PC versions of titles.

Ludum Linguarum can only be run on Windows at this time -- it has dependencies on some Windows-only libraries.

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
--production-language="<second language>" --export-path="<path to export file>" --production-word-limit=5`. The "recognition language" is the
[two-letter language code](https://en.wikipedia.org/wiki/List_of_ISO_639-1_codes) for the language you are more familiar with, 
and the "production language" is the language code for the one you're trying to learn. Examples are "en" for English, 
"fr" for French, "de" for German, "ja" for Japanese, and so on. (Flash cards will be generated for translating in both directions.) 
`export-path` is the output text file, which will be imported into Anki in the next step, and 'production-word-limit' restricts the length of
text to 5 whitespace-separated words in the target language, which can be useful for limiting the flash cards to shorter phrases or vocabulary.
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

# Frequently Asked Questions (FAQ)

## Does this really work?

**Yes!** Lots of free and commercial software and services are based around these ideas, including Duolingo, Course Hero, Memrise,
and SuperMemo. I would strongly encourage you to try it out if you are skeptical -- you'll find it starts to improve your
recall almost immediately, and daily practice will build up your vocabulary quite quickly.

## How should I use this?

You should use this as a fun supplement to other language learning resources, like classes, study groups, etc. Using flash cards
generated by this isn't going to teach you the very basics of a language, pronunciation, grammar, etc. -- it is a very effective
way of expanding your vocabulary, and seeing a huge corpus of text in a foreign language. If you've already played the game
whose text you've exported (in a language you understand), it can be quite helpful in rapidly learning the deck. It's not a 
requirement, though -- you can just as easily use this to enable you to play a game 'cold' in another language!

## What games are supported?

Check [the list of currently supported games here](http://enovales.github.io/LudumLinguarum/supported-games.html). The 
[release notes file](https://github.com/enovales/LudumLinguarum/blob/master/RELEASE_NOTES.md) also lists supported games,
along with the version number in which they were implemented.

## Can you send me the flashcard decks for game XYZ?

No. It's copyrighted content. Not gonna happen.

## Can you add support for XYZ game?

Thanks for asking! The answer is, "maybe," depending on how difficult it is to extract the content from the game.

I've [drawn up a list of games that have been investigated](http://enovales.github.io/LudumLinguarum/unsupported-games.html)
and are either definitively unsupportable (i.e. their localized text is all stored as bitmaps, etc.), or are unlikely 
to be supported due to the difficulty in reverse-engineering their file formats.

Some characteristics that might make it easier to support a particular game:

* game files are unencrypted/uncompressed/compressed in a well-known format like ZIP
* game text is in a well-structured format (XML, JSON, etc.) or in a format with existing framework/library support 
(.NET satellite localization assemblies)
* game text is in a well-documented format (like [the Bioware talk tables](http://neverwintervault.org/article/editorial/original-bioware-format-documentation-collection))
* the game is a sequel/prequel to another game that's already supported, or uses the same game engine as another 
already-supported title

I am definitely on the lookout for games that meet these criteria, as I can add support for them quickly. If you
know of games that definitely meet these criteria, please contact me (see below) and let me know!

If a game **doesn't** match any of these characteristics, it doesn't mean that it's impossible to add support, 
but it *may* be very difficult or time-consuming to do. **Code contributions and pull requests are always welcome**, so 
if you are technically inclined and sufficiently motivated, you can do it yourself!

## If I want to add support for a game, how would I go about doing so?

Look at the OneOffGamesPlugin project within the source for some small, self-contained examples. Essentially, the
plugin registers a bunch of game names that it recognizes, and then gets called by the main application to extract
the text, and generate the "cards" to be written to the SQLite database that contains all of the extracted content.
SimpleGames.fs contains the simplest implementations of extraction -- some of them are just a few lines of code!

## How can I contact you?
The project has a Twitter account, [@LudumLinguarum](https://twitter.com/LudumLinguarum). 
You can also contact me (Erik Novales) at [@yankeefinn](https://twitter.com/yankeefinn).