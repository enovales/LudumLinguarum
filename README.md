# LudumLinguarum

Ludum Linguarum is a set of tools and recipes to extract localized content (currently just text) from a set of supported 
games, which can then be turned into flash cards for use with the following [spaced repetition](https://en.wikipedia.org/wiki/Spaced_repetition) software:

1. [Anki](https://apps.ankiweb.net/), one of the most popular spaced repetition software programs, with clients for all major desktop and mobile platforms.
2. [SuperMemo](https://www.supermemo.com/)
3. [Mnemosyne](https://mnemosyne-proj.org/)
4. [AnyMemo](https://anymemo.org/)
5. [Brainscape](https://www.brainscape.com/), a web and mobile app. Cards are exported as a CSV file that can be bulk-imported via its "Import Flashcards" feature.

In other words, you can spice up your language learning regimen by drilling yourself with flash cards containing text from video games!

[The list of 81 currently supported games can be found here](supported-games.md).
Currently only PC games are supported, although there's no technical reason other platforms couldn't be supported -- it's just simpler to work on PC versions of titles.

Ludum Linguarum can run on Windows, Linux, and OS X! (Some titles may only be supported under Windows.)

# Anki Quick Start

This walkthrough covers setting up Ludum Linguarum, the Anki desktop client, importing text from a game, exporting it to
a format that Anki can recognize, and then importing it into Anki and using the flashcards to drill.

1. Download and extract the latest version of Ludum Linguarum, [from the list of releases above](https://github.com/enovales/LudumLinguarum/releases).
2. Download the [Anki desktop client](http://ankisrs.net/) and install it.
3. Ensure that you have at least [one of the supported games](https://github.com/enovales/LudumLinguarum/blob/master/docsrc/content/supported-games.md) installed.
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

# SuperMemo Quick Start

This walkthrough covers setting up Ludum Linguarum, importing text from a game, exporting it to a format that will work with 
SuperMemo's online importer, and then importing it and using the flashcards to drill.

1. Download and extract the latest version of Ludum Linguarum, [from the list of releases above](https://github.com/enovales/LudumLinguarum/releases).
2. Ensure that you have at least [one of the supported games](https://github.com/enovales/LudumLinguarum/blob/master/docsrc/content/supported-games.md) installed.
3. Open up a command prompt window, in the directory to which you extracted Ludum Linguarum.
4. Type `LudumLinguarum list-supported-games` to find the name of the game that you want to extract.
5. Type `LudumLinguarum import --game="<game name>" --game-dir="<root directory of game>"` to import the game text. (Some games 
that only support a single language with their installed data may require you to specify the language with `--language-tag=<language tag>`.)
6. Type `LudumLinguarum export-supermemo --game="<game name>" --recognition-language="<first language>" 
--production-language="<second language>" --export-path="<path to export file>" --production-word-limit=5`. The "recognition language" is the
[two-letter language code](https://en.wikipedia.org/wiki/List_of_ISO_639-1_codes) for the language you are more familiar with, 
and the "production language" is the language code for the one you're trying to learn. Examples are "en" for English, 
"fr" for French, "de" for German, "ja" for Japanese, and so on. (Flash cards will be generated for translating in both directions.) 
`export-path` specifies the output text file (or files -- your export will be split up into files of 99 cards each, which matches SuperMemo's limit
of how many cards you can import at once), which will be imported into SuperMemo in the next step, and 'production-word-limit' restricts the length of
text to 5 whitespace-separated words in the target language. This can be useful for limiting the flash cards to shorter phrases or vocabulary.
7. Open up the SuperMemo website. Select the **Course Editor** from the drop-down menu.
8. Click on the **+** button at the top (next to the list of courses) to create a new course, and give it a name.
9. For each file generated in Step 6, do the following:
  * Click the **Import** button at the bottom. Select **Tab** under "Question and Answer".
  * Open up the file.
  * Copy all of the text from the file.
  * Paste it into the SuperMemo import dialog.
  * Verify that the card contents are correct, and hit the **Import** button at the bottom.
12. Exit, and select the new deck from SuperMemo's main menu. Now you're ready to practice! You can read more about how to most effectively use 
SuperMemo [on their main web site](https://www.supermemo.com/).

# Mnemosyne Quick Start

This walkthrough importing text from a game, exporting it to
a format that Mnemosyne can recognize, and then importing it into Mnemosyne and using the flashcards to drill.

1. Download and extract the latest version of Ludum Linguarum, [from the list of releases above](https://github.com/enovales/LudumLinguarum/releases).
2. Download the [Mnemosyne client](https://mnemosyne-proj.org/) and install it.
3. Ensure that you have at least [one of the supported games](https://github.com/enovales/LudumLinguarum/blob/master/docsrc/content/supported-games.md) installed.
4. Open up a command prompt window, in the directory to which you extracted Ludum Linguarum.
5. Type `LudumLinguarum list-supported-games` to find the name of the game that you want to extract.
6. Type `LudumLinguarum import --game="<game name>" --game-dir="<root directory of game>"` to import the game text. (Some games 
that only support a single language with their installed data may require you to specify the language with `--language-tag=<language tag>`.)
7. Type `LudumLinguarum export-mnemosyne --game="<game name>" --recognition-language="<first language>" 
--production-language="<second language>" --export-path="<path to export file>" --production-word-limit=5`. The "recognition language" is the
[two-letter language code](https://en.wikipedia.org/wiki/List_of_ISO_639-1_codes) for the language you are more familiar with, 
and the "production language" is the language code for the one you're trying to learn. Examples are "en" for English, 
"fr" for French, "de" for German, "ja" for Japanese, and so on. (Flash cards will be generated for translating in both directions.) 
`export-path` is the output text file, which will be imported into Anki in the next step, and 'production-word-limit' restricts the length of
text to 5 whitespace-separated words in the target language, which can be useful for limiting the flash cards to shorter phrases or vocabulary.
8. Open up the Mnemosyne client.
9. Select the **Import** menu item under the **File** menu. Select the file you exported in step 7, and make sure that the file format selected is **Tab-separated text files**. You may want to add a tag to these cards, so you can bulk-modify them after import.
10. Click the **Ok** button. After the import is complete, you may want to select the cards (under **Cards** -> **Browse Cards...**), and change the card type from **Front-to-back only** to **Front-to-back and back-to-front**.
11. You are now ready to study with Mnemosyne! Read its documentation to find more information about how to use the client.

# Brainscape Quick Start

This walkthrough covers importing text from a game, exporting it to a CSV file that Brainscape can import in one shot, and then using the flashcards to drill.

1. Download and extract the latest version of Ludum Linguarum, [from the list of releases above](https://github.com/enovales/LudumLinguarum/releases).
2. Ensure that you have at least [one of the supported games](https://github.com/enovales/LudumLinguarum/blob/master/docsrc/content/supported-games.md) installed.
3. Open up a command prompt window, in the directory to which you extracted Ludum Linguarum.
4. Type `LudumLinguarum list-supported-games` to find the name of the game that you want to extract.
5. Type `LudumLinguarum import --game="<game name>" --game-dir="<root directory of game>"` to import the game text. (Some games that only support a single language with their installed data may require you to specify the language with `--language-tag=<language tag>`.)
6. Type `LudumLinguarum export-brainscape --game="<game name>" --recognition-language="<first language>" --production-language="<second language>" --export-path="<path to export .csv file>" --production-word-limit=5`. The arguments work the same way as in the Anki quick start above: the "recognition language" is the language you already know, the "production language" is the one you are trying to learn, and `production-word-limit` restricts cards to short phrases in the target language. The export is a UTF-8 CSV file with two columns, which Brainscape reads as question and answer. Cards that can be reversed are exported in both directions. If more than 500 cards are exported, the output is split into multiple CSV files of at most 500 records each; the first file keeps the given name, and additional files are numerically suffixed (`_001`, `_002`, ...).
7. Open the [Brainscape website](https://www.brainscape.com/) and create a class with a deck inside it (or open an existing deck).
8. Click **Add Cards** and choose **Import Flashcards**, then upload the CSV file exported in step 6 (if the export was split into multiple files, upload each one into the same deck).
9. Select the new deck and start studying!

# Other notes

* If you want to focus on particular types of cards (i.e. short vocabulary words, instead of translating dialogue), don't hesitate
to use Anki's "bury" or "delete" functions -- "bury" will send the card to the bottom of the deck, while "delete" will completely
remove it. Also, depending on whether a game's extraction recipe breaks it down this way, you may be able to filter on extraction 
by "lesson" name -- see the command-line help for more details about this.

* Depending on the game, sometimes the extraction isn't 100% clean. Please let me know if you run into problems -- and in the
meantime, you can always delete incorrect cards from your decks so you can continue working with them in the meantime.

# Frequently Asked Questions (FAQ)

## Does this really work?

**Yes!** Lots of free and commercial software and services are based around these ideas, including Duolingo, Course Hero, and Memrise. 
I would strongly encourage you to try it out if you are skeptical -- you'll find it starts to improve your
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

---

## Builds


GitHub Actions |
:---: |
[![GitHub Actions](https://github.com/enovales/LudumLinguarum/workflows/Build%20master/badge.svg)](https://github.com/enovales/LudumLinguarum/actions?query=branch%3Amaster) |
[![Build History](https://buildstats.info/github/chart/enovales/LudumLinguarum)](https://github.com/enovales/LudumLinguarum/actions?query=branch%3Amaster) |

## NuGet

Package | Stable | Prerelease
--- | --- | ---
LudumLinguarum | [![NuGet Badge](https://buildstats.info/nuget/LudumLinguarum)](https://www.nuget.org/packages/LudumLinguarum/) | [![NuGet Badge](https://buildstats.info/nuget/LudumLinguarum?includePreReleases=true)](https://www.nuget.org/packages/LudumLinguarum/)


---

### Developing

Make sure the following **requirement** is installed on your system:

- [.NET SDK](https://dotnet.microsoft.com/download) 10.0 or higher (see `global.json` for the exact version)

or

- [VSCode Dev Container](https://code.visualstudio.com/docs/remote/containers)

---

### Building and testing

```sh
dotnet build   # build the solution
dotnet test    # run the tests
```

CI (`.github/workflows/build.yml`) runs the same commands on every push and pull request.

---

### Releasing

Releases are published on GitHub from a git tag, and the changelog follows [Keep a Changelog](https://keepachangelog.com/).

1. In `CHANGELOG.md`, move the entries under `## [Unreleased]` into a new versioned section, e.g. `## [2.0.0] - 2026-11-09`, and make sure the compare links at the bottom of the file cover the new version.
2. Commit and push to `main`, then tag the release commit and push the tag:

   ```sh
   git tag v2.0.0
   git push origin v2.0.0
   ```

3. On GitHub, go to **Releases -> Draft a new release**, choose the `v2.0.0` tag, use the version as the release title, paste the new `CHANGELOG.md` section as the description, and publish.

There is no automated packaging pipeline: CI only builds and tests, and binaries for end users are produced locally with `dotnet publish -c Release`.

---

# Third-Party Licenses and Acknowledgments

## FParsec

    Copyright (c) 2007‒2017, Stephan Tolksdorf. All rights reserved.

    Redistribution and use in source and binary forms, with or without modification, are permitted provided that the following conditions are met:

    Redistributions of source code must retain the above copyright notice, this list of conditions and the following disclaimer.
    Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer in the documentation and/or other materials provided with the distribution.
    This software is provided by the copyright holders “as is” and any express or implied warranties, including, but not limited to, the implied warranties of merchantability and fitness for a particular purpose are disclaimed. In no event shall the copyright holders be liable for any direct, indirect, incidental, special, exemplary, or consequential damages (including, but not limited to, procurement of substitute goods or services; loss of use, data, or profits; or business interruption) however caused and on any theory of liability, whether in contract, strict liability, or tort (including negligence or otherwise) arising in any way out of the use of this software, even if advised of the possibility of such damage.

## json-everything

    MIT License

    Copyright (c) 2022 Greg Dennis

    Permission is hereby granted, free of charge, to any person obtaining a copy
    of this software and associated documentation files (the "Software"), to deal
    in the Software without restriction, including without limitation the rights
    to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
    copies of the Software, and to permit persons to whom the Software is
    furnished to do so, subject to the following conditions:

    The above copyright notice and this permission notice shall be included in all
    copies or substantial portions of the Software.

    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
    IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
    FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
    AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
    LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
    OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
    SOFTWARE.