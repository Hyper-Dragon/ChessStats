# ChessStats for chess.com

## About

ChessStats is a console app used to retrieve monthly play time, ratings and top openings for any chess.com user. Note that variant game types are not included in the stats (displayed as 'X' during game retrieval) and unrated game ('NR') information is time only. 

## Installation

Download the latest version from the [Releases](https://github.com/Hyper-Dragon/ChessStats/releases) page, extract _ChessStats.exe_ and put it where you want it to go.  Note that the cache and reporting directories are created relative to the executable's location. 

## Usage

``` Powershell
chessstats [chessdotcom username]
```

or

``` Powershell
chessstats -refresh
```

or just double click the _exe_ and you will be prompted for a chess.com username.

## Version History

### Version 0.7

* CAPS scores, limited to the last 20 games, now included
* Openings for the last 40 games table added
* Various Html improvements including:
  * Favicon added to pages
  * Image used for the background
  * Embedded fonts
  * Dark tables
* The index file is only generated once per run
* Fixed v0.6 missing assembly issue
* Removed Microsoft.CodeAnalysis.FxCopAnalyzers (deprecated)
* Switched to .Net 6.0

### Version 0.6

* Graphs for ratings and monthly win/loss averages (included in Html Reports)
* Html index file generation _([location of executable]/ChessStatsResults/index.html)_
* Several minor fixes
* CAPS scores removed due to chess.com site changes

### Version 0.5

* Output files written to a reporting directory _([location of executable]/ChessStatsResults/[Username]/)_ containing:
  * A full (self-contained) HTML Report
  * The original text report
  * Full PGN files (all games) for each time control
  * CAPs Data in TSV format for easy spreadsheet import
* A refresh data option (for each user in the reporting directory)
* Caching for previously retrieved game and CAPs data at _([location of executable]/ChessStatsCache/CacheV1/[Username])_
* Error handling improvements
* Several minor display fixes

### Version 0.4 

* CAPS averages are broken down by time control.  
  * These are available for games that have been analysed on the chess.com website (games with no analysis are marked with '-' on ingest). 

## Known Issues

* Version 0.7
  * None (yet)
* Version 0.6
  * CAPS scores are not available due to Chess.com site changes
  * Error: An assembly specified in the application dependencies manifest (ChessStats.deps.json) was not found 
    * Delete the folder C:\Users\<username>\AppData\Local\Temp\ .net\ and rerun
  * Error: Unable to write index.html 
    * Make sure that index.html is not in use and rerun

## Saying Thank You

If you find this useful and want to say thanks [just send me a fun trophy or two or three](https://www.chess.com/member/hyper-dragon) over on chess.com :smiley:

## Example Output (HTML)

![Sample Report](https://raw.githubusercontent.com/Hyper-Dragon/ChessStats/master/HtmlReportExample3.png)
