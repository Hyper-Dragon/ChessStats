# ChessStats for chess.com

## About

ChessStats is a console app used to retrieve monthly play time, ratings and top openings for any Chess.com user. Note that variant game types are not included in the stats (displayed as 'X' during game retrieval) and unrated game ('NR') information is time only.

The HTML reports generated are standalone with all assets (images/fonts) embedded in the page.  This means they can be sent to others, or indeed just opened as a document.  

Personally, I sync my ChessStats folder with dropbox so I can read the reports directly on my tablet from the Dropbox app.

## Installation

Download the latest version from the [Releases](https://github.com/Hyper-Dragon/ChessStats/releases) page, extract _ChessStats.exe_/_ChessStats_ and put it where you want it to go.  **Note that the cache and reporting directories are created relative to the executable's location**.

## Usage

``` Powershell
chessstats [chessdotcom username]
```

or

``` Powershell
chessstats -refresh
```

or just double click the _exe_ and you will be prompted for a Chess.com username.

![Sample Run](assets/images/RunningConsole.gif)

## Docker

Every release publishes a container image for `linux/amd64` and `linux/arm64` to the GitHub Container Registry. It runs ChessStats, refreshes the reports on a schedule and serves them with nginx, so a single container is enough to publish a set of players' stats.

```
docker pull ghcr.io/hyper-dragon/chessstats:latest
```

The package is public, so no registry login is needed. Use a version tag (`ghcr.io/hyper-dragon/chessstats:0.8.3`) if you want a fixed release; `latest` follows the most recent one. The image contains a self-contained single-file build of the tool on top of the .NET runtime dependencies, so nothing else has to be installed.

### Running it

```
docker run -d --name chessstats \
  -p 18080:80 \
  -e CHESS_USERS="Hyper-Dragon GothamChess" \
  -v chessstats-results:/app/ChessStatsResults \
  -v chessstats-cache:/app/ChessStatsCache \
  ghcr.io/hyper-dragon/chessstats:latest
```

Then browse to `http://localhost:18080/` for the index page. The first report is generated on start, so allow a few minutes on an empty volume - `docker logs -f chessstats` shows the progress.

| Environment variable | Default | Purpose |
| --- | --- | --- |
| `CHESS_USERS` | _(empty)_ | Space separated usernames to report on. Each one is created as a directory under `/app/ChessStatsResults`. |
| `REFRESH_INTERVAL` | `86400` | Seconds between refreshes. |

| Volume | Contents |
| --- | --- |
| `/app/ChessStatsResults` | The generated reports and `index.html` - this is what nginx serves. |
| `/app/ChessStatsCache` | The downloaded monthly game archives, so restarts do not re-download history. |

### Choosing the accounts

A directory under `/app/ChessStatsResults` **is** an account to report on - `-refresh` works from that directory list. `CHESS_USERS` simply creates those directories at start up, so either approach works, and pre-seeding the volume is the more declarative of the two:

```
docker run --rm -v chessstats-results:/results alpine \
  sh -c "mkdir -p /results/Hyper-Dragon /results/GothamChess"
```

Use the spelling Chess.com reports for the account (the name in its profile URL). A directory seeded in a different case is adopted and renamed, so `hyper-dragon` becomes `Hyper-Dragon` on the first run. Removing an account is just removing its directory.

### Notes

- Reports are large - tens of megabytes for an active player - because everything is embedded so they work offline. nginx serves them gzipped.
- Give the container memory headroom. A very active account holds its whole game list plus the generated report in memory at once.
- Exit codes are `0` (ok), `1` (finished with warnings) and `2` (fatal).

## Offline Reports & Network Access

Every report is a single self-contained HTML file: graphs, images, fonts and CSS are all embedded, so it can be copied anywhere and opened without a network connection.

To make that possible the report graphics (the player placeholder image, the heading icon and the Montserrat 700/800 fonts) are compiled into the executable as embedded resources instead of being downloaded at run time. Chess.com retired the CDN those files used to be served from, which previously made every run fail with `521` before any data had been retrieved (issue [#27](https://github.com/Hyper-Dragon/ChessStats/issues/27)).

The only network access during a run is the Chess.com public API itself, plus the player's avatar - and if that avatar cannot be downloaded the built-in placeholder is used instead.

## Version History

### Version 0.8.3
* Report graphics (images and fonts) are now embedded in the executable instead of being
  downloaded at run time. Chess.com retired the CDN they were fetched from, which made every
  run fail before any data was retrieved (issue #27)
* A player with no avatar now falls back to the built-in placeholder rather than failing the run
* An account with no completed games is reported as a single notice and no longer fails the run
* A results directory seeded by hand is matched case-insensitively and adopted, so report links
  stay valid on case-sensitive filesystems
* Exit codes are 0 (ok), 1 (completed with warnings) and 2 (fatal) - negative codes both
  collapse to 254 on Linux
* Sends a contactable User-Agent, as required by the Chess.com API policy
* Assembly/file version metadata now matches the release number
* Moved to .NET 10 (.NET 6 is out of support)

### Version 0.8.2
* Fixed 403 (Forbidden) responses from the Chess.com API by sending a User-Agent

### Version 0.8.1
* Improved HTML Report
  * Only display stats graphs for the time controls played by the CDC user

### Version 0.8 
* Full CAPS Scores for all games analysed on Chess.com
  * All games graph
  * 3 Game Rolling Average
  * 10 Game Rolling Average
* Improved Graph Rendering (.svg)
* Removed the dependency on libgdiplus (linux only)
* Changed the report section ordering
* Added a linux arm build
 
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
* CAPS scores removed due to Chess.com site changes

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
  * These are available for games that have been analysed on the Chess.com website (games with no analysis are marked with '-' on ingest).

## Known Issues

* Version 0.8
  * None (yet)
* Version 0.7
  * None
* Version 0.6
  * CAPS scores are not available due to Chess.com site changes
  * Error: An assembly specified in the application dependencies manifest (ChessStats.deps.json) was not found 
    * Delete the folder C:\Users\<username>\AppData\Local\Temp\ .net\ and rerun
  * Error: Unable to write index.html 
    * Make sure that index.html is not in use and rerun

## License

This is free and unencumbered software released into the public domain.

Anyone is free to copy, modify, publish, use, compile, sell, or distribute this software, either in source code form or as a compiled binary, for any purpose, commercial or non-commercial, and by any means.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

For more information, please refer to [unlicense.org](https://unlicense.org).

## Saying Thank You

If you find this useful and want to say thanks [just send me a fun trophy or two or three](https://www.chess.com/member/hyper-dragon) over on chess.com.  If you find it really useful then consider hitting the [donate](https://github.com/sponsors/Hyper-Dragon) button at the top of the page.

## Example Output (HTML)

![Sample Report](assets/images/SampleReport.png)
