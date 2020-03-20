# ChessStats for [Chess.com](https://chess.com)

## Usage
ChessStats is a console app used to retrieve monthly play time, ratings and top openings for any chessdotcom user. Note that variant game types are not included in the stats (displayed as 'X' during game retrieval) and unrated game ('NR') information is time only. 

If you find this useful and want to say thanks just send me a fun trophy over on chess.com ;-)

```
chessstats [chessdotcom username]
```
## Example Output Fragments

```
> chessstats fabianocaruana
```

<pre>
====================== Live Chess Report for FabianoCaruana - 19/03/2020 =======================

========== Openings Playing As White >1 (Max 15) ===========  
  
>A00-Mieses Opening                                                          |   98
>B23-Sicilian Defense Closed                                                 |   32
>B06-Modern Defense with                                                     |   23
>C00-French Defense Knight Variation Two Knights Variation                   |   21
</pre>
...
<pre>
========== Openings Playing As Black >1 (Max 15) ===========

A40-Queens Pawn Opening Horwitz Defense                                     |   54
C00-French Defense Normal Variation                                         |   35
A13-English Opening Agincourt Defense                                       |   26
E00-Indian Game East Indian Defense                                         |   23
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
=================== Time Played by Month ===================

Month                  | Play Time
-----------------------+-----------
2013-03                |  24:48:19
2013-04                |   8:01:49
2013-05                |   1:55:51
2013-06                |   3:05:15
</pre>
...
<pre>
=============== Total Play Time (Live Chess) ===============

Time Played (hh:mm:ss): 149:50:52
</pre>

## Known Issues
- Games of type 'oddschess' generate/display error message [Raised](https://github.com/nullablebool/ChessDotComSharp/issues/1)

## Dependencies

- [ChessDotComSharp](https://github.com/nullablebool/ChessDotComSharp) @nullablebool

## Acknowledgements

- [ChessDotCom](https://github.com/ChessCom) @ChessCom

