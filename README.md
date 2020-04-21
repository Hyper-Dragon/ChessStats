# ChessStats for [Chess.com](https://chess.com)

## About
ChessStats is a console app used to retrieve monthly play time, ratings and top openings for any chessdotcom user. Note that variant game types are not included in the stats (displayed as 'X' during game retrieval) and unrated game ('NR') information is time only. 

The __version 0.4__ update included CAPS averages broken down by time control.  These are available for games that have been analysed on the chess.com website (games with no analysis are marked with '-' on ingest).  

__Version 0.5__ is a major release with numerous improvements over v0.4 including:

* Output files written to a reporting directory _([location of executable]/ChessStatsReporting/[Username])_ containing:
  * Full (self-contained) HTML Report
  * The original text report
  * Full PGN files (all games) for each time control
  * CAPs Data in TSV format for easy spreadsheet import
* A refresh data option (for each user in the reporting directory)
* Caching for previously retrieved game and CAPs Data at _([location of executable]/ChessStatsCache/CacheV1/[Username])_
* Error handling improvements
* Several minor display fixes

## Saying Thank You
If you find this useful and want to say thanks just send me a fun trophy over on chess.com :smiley:

## Usage
```
chessstats [chessdotcom username]
```
or 
```
chessstats -refresh
```

## Example Output (HTML)


## Example Output Fragments (Text)

```
> chessstats fabianocaruana
```

<pre>
======================== Live Chess Report for FabianoCaruana - 20/03/2020 =========================

======== Openings Occurring More Than Once (Max 15) ========

Playing As White                                                        | Tot.
------------------------------------------------------------------------+------
A00-Mieses Opening                                                      |   98
B23-Sicilian Defense Closed                                             |   32
B06-Modern Defense with                                                 |   23
C00-French Defense Knight Variation Two Knights Variation               |   21
</pre>
...
<pre>
Playing As Black                                                        | Tot.
------------------------------------------------------------------------+------
A40-Queens Pawn Opening Horwitz Defense                                 |   54
C00-French Defense Normal Variation                                     |   35
A13-English Opening Agincourt Defense                                   |   26
E00-Indian Game East Indian Defense                                     |   23
</pre>
...
<pre>
============= Time Played by Time Class/Month ==============

Time Class/Month  | Play Time | Rating Min/Max/+-  | Vs Min/BestWin/Max | Win  | Loss | Draw | Tot.
------------------+-----------+--------------------+--------------------+------+------+------+------
Blitz     2013-03 |  13:32:52 | 1363 | 2610 | 1247 | 1186 | 2517 | 2580 |  158 |   12 |    6 |  176
Blitz     2013-04 |   3:35:46 | 2599 | 2630 |   31 | 2220 | 2564 | 2571 |   30 |    7 |    8 |   45
Blitz     2013-05 |   0:56:56 | 2602 | 2639 |   37 | 2316 | 2328 | 2523 |    6 |    3 |    1 |   10
Blitz     2013-06 |   2:13:00 | 2571 | 2606 |   35 | 2403 | 2474 | 2484 |    7 |    5 |    2 |   14
Blitz     2014-05 |   0:48:46 | 2606 | 2667 |   61 | 2507 | 2523 | 2523 |    6 |    1 |    1 |    8
Blitz     2016-02 |   2:37:17 | 2651 | 2737 |   86 | 2481 | 2506 | 2511 |    9 |    2 |    4 |   15
</pre>
...
<pre>
========= Time Played by Month (All Time Controls) =========

Month             |  Play Time  | Cumulative  |  For Year
------------------+-------------+-------------+-------------
2013-03           |    24:48:19 |    24:48:19 |    24:48:19
2013-04           |     8:01:49 |    32:50:08 |    32:50:08
2013-05           |     1:55:51 |    34:45:59 |    34:45:59
2013-06           |     3:05:15 |    37:51:14 |    37:51:14
2013-12           |     0:51:21 |    38:42:35 |    38:42:35
------------------+-------------+-------------+-------------
2014-05           |     2:10:52 |    40:53:27 |     2:10:52
------------------+-------------+-------------+-------------
2016-02           |     3:02:01 |    43:55:28 |     3:02:01
</pre>
...
<pre>
========== CAPS Scoring (Month Average > 4 Games) ==========

                  |      Bullet     |     Blitz     |     Rapid
Month             |   White | Black | White | Black | White | Black
------------------+---------+-------+-------+-------+-------+-------
2013-03           |   77.5  |   -   |   -   |   -   |   -   |   -
2017-08           |     -   | 88.44 | 90.09 | 97.13 |   -   |   -
2018-12           |     -   |   -   | 92.45 | 93.46 |   -   |   -
2019-08           |     -   |   -   |   -   |   -   | 94.43 | 96.17
2020-04           |     -   |   -   | 94.94 |   -   |   -   |   -
</pre>
...
<pre>
========== CAPS Scoring (Rolling 10 Game Average) ==========

Control/Side      |   <-Newest                                                             Oldest->
------------------+---------------------------------------------------------------------------------
Bullet White      |   93.08 | 93.4  | 92.63 | 93.64 | 92.79 | 90.5  | 89.78
Bullet Black      |   88.67 | 86.87 | 88.1  | 87.75
Blitz White       |   88.78 | 88.17 | 87.76 | 87.79 | 87.59 | 87.27 | 89.86 | 89.97 | 89.42 | 92.45
Blitz Black       |   90.77 | 91.44 | 90.7  | 91.03 | 90.57 | 91.06 | 91.68 | 91.08 | 93.68 | 93.47
</pre>
...
<pre>
=============== Total Play Time (Live Chess) ===============

Time Played (hh:mm:ss): 149:50:52
</pre>

## Known Issues
- Games of type 'oddschess' generate/display error messages.  Fixed in solution and [Raised](https://github.com/nullablebool/ChessDotComSharp/issues/1)

## Dependencies

- [ChessDotComSharp](https://github.com/nullablebool/ChessDotComSharp) @nullablebool
- Chess.com Api
- Chess.com website HTML for CAPs extraction

## Acknowledgements

- [ChessDotCom](https://github.com/ChessCom) @ChessCom
- Covid-19 & Social Isolation
- Thanks to chess.com user maxmlynek2 for bug spotting

