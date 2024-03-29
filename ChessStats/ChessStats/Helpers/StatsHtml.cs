﻿using System;
using System.Linq;
using System.Text;

namespace ChessStats.Helpers
{
    internal class StatsHtml
    {
        public static string GetHtmlTail(Uri chessdotcomUrl, string versionNumber, string projectLink)
        {
            return chessdotcomUrl == null
                ? throw new ArgumentNullException(nameof(chessdotcomUrl))
                : $"<div class='footer'><br/><hr/><i>Generated by ChessStats (for <a href='{chessdotcomUrl.OriginalString}'>Chess.com</a>)&nbsp;ver. {versionNumber}<br/><a href='{projectLink}'>{projectLink}</a></i><br/><br/><br/></div>";
        }

        public static string GetHtmlTop(string pageTitle, string backgroundImage, string favIconImage, string font700Fragment, string font800Fragment, int controlsPlayed=3)
        {
            StringBuilder htmlReport = new();
            _ = htmlReport.AppendLine("<!DOCTYPE html>")
                          .AppendLine("<html lang='en'>")
                          .AppendLine("  <head>")
                          .AppendLine("    <meta charset='utf-8'>")
                          .AppendLine($"    <title>{pageTitle}</title>")
                          .AppendLine($"    <link rel='shortcut icon' type='image/png' href={favIconImage}/>")
                          .AppendLine("    <meta name='generator' content='ChessStats'> ")
                          .AppendLine("    <meta name='viewport' content='width=device-width, initial-scale=1.0'>")
                          .AppendLine("    <style>")
                          .AppendLine("      *                                                             {margin: 0;padding: 0;}")
                          .AppendLine("      @media screen and (max-width: 1000px) and (min-width: 768px)  {.priority-4{display:none;}}")
                          .AppendLine("      @media screen and (max-width: 768px)  and (min-width: 600px)  {.priority-4{display:none;}.priority-3{display:none;}}")
                          .AppendLine("      @media screen and (max-width: 600px)                          {.priority-4{display:none;}.priority-3{display:none;}.priority-2{display:none;}}")
                          .AppendLine($"      @font-face                                                    {{ {font700Fragment} }}")
                          .AppendLine($"      @font-face                                                    {{ {font800Fragment} }}")
                          .AppendLine($"      body                                                          {{background-color: #232323 ; width: 90%; margin: auto; font-family: -apple-system,BlinkMacSystemFont,Segoe UI,Helvetica,Arial,sans-serif; background-image: url({backgroundImage}); }}")
                          .AppendLine("      h1                                                            {font-family: Montserrat; font-weight: 800; padding: 10px;text-align: left;font-size: 37px; color: hsla(0,0%,100%,.65);}")
                          .AppendLine("      h1 small                                                      {font-family: Montserrat; font-weight: 700; font-size: 15px; vertical-align: bottom}")
                          .AppendLine("      a:link                                                        {color: rgb(217, 233, 238);}")
                          .AppendLine("      a:visited                                                     {color: rgb(217, 233, 238);}")
                          .AppendLine("      a:hover                                                       {color: #FCFC0C}")
                          .AppendLine("      a:active                                                      {color: #C0F0FC}")
                          .AppendLine("      a.headerLink                                                  {color: #e58b09}")
                          .AppendLine("      h2                                                            {font-family: Montserrat; font-weight: 800;clear:left;padding: 5px;text-align: left;font-size: 20px;background-color: rgba(0,0,0,.13);color: hsla(0,0%,100%,.65);}")
                          .AppendLine("      table                                                         {width: 100%;table-layout: fixed ;border-collapse: collapse; overflow-x:auto; }")
                          .AppendLine("      thead                                                         {font-family: Montserrat; font-weight: 800;text-align: center;background: #769656;color: white;font-size: 15px; font-weight: bold;}")
                          .AppendLine("      thead tr                                                      {height:27px} ")
                          .AppendLine("      tbody                                                         {font-family: monospace; text-align: center;font-size: 14px;}")
                          .AppendLine("      td                                                            {padding-right: 0px;}")
                          .AppendLine("      td:nth-child(1)                                               {padding-left:10px; text-align: left; width: 105px ; font-weight: bold;}")
                          .AppendLine("      tbody tr:nth-child(odd)                                       {background-color:  rgba(255,255,255,0.25); color: rgb(245,245,245);}")
                          .AppendLine("      tbody tr:nth-child(even)                                      {background-color:  rgba(255,255,255,0.15); color: rgb(245,245,245);}")
                          .AppendLine("      .active                                                       {background-color: rgba(118,150,86, 0.6)}")
                          .AppendLine("      .inactive                                                     {background-color: rgba(167,166,162, 0.6)}")
                          .AppendLine("      .headBox                                                      {background-color: rgba(0,0,0,.13)}")
                          .AppendLine("      .headRow                                                      {display: grid; grid-template-columns: 200px auto; grid-gap: 0px; border:0px; height: auto; padding: 0px; }")
                          .AppendLine("      .headRow > div                                                {padding: 0px; }")
                          .AppendLine("      .headBox img                                                  {vertical-align: middle}")
                          .AppendLine($"      .ratingRow                                                    {{display: grid;grid-template-columns: {string.Join(" ","auto auto auto".Split(" ").Take(controlsPlayed))} ;grid-gap: 20px;padding: 10px;}}")
                          .AppendLine("      .ratingRow > div                                              {font-family: Montserrat; font-weight: 700; text-align: center;  padding: 0px;  color: whitesmoke;  font-size: 15px;  font-weight: bold;}")
                          .AppendLine("      .ratingBox                                                    {cursor: pointer;}")
                          .AppendLine($"      .graphRow                                                     {{display: grid;grid-template-columns: {string.Join(" ", "auto auto auto".Split(" ").Take(controlsPlayed))} ;grid-gap: 10px;padding: 5px;}}")
                          .AppendLine("      .graphRow > div                                               {font-family: Montserrat; font-weight: 700; text-align: center;  padding: 0px;  color: whitesmoke;  font-size: 15px;  font-weight: bold;}")
                          .AppendLine("      .graphBox img                                                 { width:100%; height:auto; object-fit: cover; }")
                          .AppendLine("      .graphCapsRow                                                 {display: grid;grid-template-columns: 60% auto;grid-gap: 10px;padding: 5px;}")
                          .AppendLine("      .graphCapsRow>div                                             {font-family: Montserrat;font-weight: 700;text-align: center;padding: 0px;color: whitesmoke;font-size: 15px;font-weight: bold;}")
                          .AppendLine("      .graphCapsBox img                                             {max-width: 100%; width: auto;height: auto;}")  
                          .AppendLine("      .yearSplit                                                    {border-top: thin dotted; border-color: #1583b7;}")
                          .AppendLine("      .higher                                                       {background-color: hsla(120, 100%, 50%, 0.25);}")
                          .AppendLine("      .lower                                                        {background-color: hsla(0, 100%, 70%, 0.4);}")
                          .AppendLine("      .whiteOpeningsTable thead td:nth-child(1)                     {font-family: Montserrat; font-weight: 800;font-weight: bold; font-size:14px; }")
                          .AppendLine("      .blackOpeningsTable thead td:nth-child(1)                     {font-family: Montserrat; font-weight: 800;font-weight: bold; font-size:14px; }")
                          .AppendLine("      .whiteOpeningsTable td:nth-child(1)                           {padding-left:10px; text-align: left; width:50%; font-family: Montserrat; font-weight: 700; font-weight: normal; font-size:11px; }")
                          .AppendLine("      .blackOpeningsTable td:nth-child(1)                           {padding-left:10px; text-align: left; width:50%; font-family: Montserrat; font-weight: 700; font-weight: normal; font-size:11px; }")
                          .AppendLine("      .capsRollingTable thead td:nth-child(2)                       {text-align: left;}")
                          .AppendLine("      .capsRollingTable tbody td:nth-child(1)                       {font-size: 14px;font-weight: bold;}")
                          .AppendLine("      .playingStatsTable tbody td:nth-child(1)                      {font-size: 14px;font-weight: bold;}")
                          .AppendLine("      .playingStatsMonthTable tbody td:nth-child(1)                 {font-size: 14px;font-weight: bold;}")
                          .AppendLine("      .playingStatsTable tbody td:nth-child(5)                      {border-right: thin solid; border-color: #1583b7;}")
                          .AppendLine("      .playingStatsTable tbody td:nth-child(8)                      {border-left: thin dotted; border-color: #1583b7;}")
                          .AppendLine("      .playingStatsTable tbody td:nth-child(11)                     {border-left: thin dotted; border-color: #1583b7;}")
                          .AppendLine("      .playingStatsTable tbody td:nth-child(13)                     {border-left: thin solid; border-color: #1583b7;}")
                          .AppendLine("      .indextab                                                     {table-layout: fixed; white-space: nowrap; width:100%}")
                          .AppendLine("      .indextab td                                                  {width:auto; white-space: nowrap;overflow: hidden;text-overflow: ellipsis;}")
                          .AppendLine("      .indextab td:nth-child(1)                                     {width:250px; white-space: nowrap;overflow: hidden;text-overflow: ellipsis;}")
                          .AppendLine("      .oneColumn                                                    {float: left;width: 100%;}")
                          .AppendLine("      .oneRow:after                                                 {content: ''; display: table; clear: both;}")
                          .AppendLine("      .footer                                                       {font-family: Montserrat; font-weight: 700; text-align: right;color: white; font-size: 11px}")
                          .AppendLine("      .footer a                                                     {color: #e58b09;}")
                          .AppendLine("    </style>")
                          .AppendLine("  </head>")
                          .AppendLine("  <body>");

            return htmlReport.ToString();
        }
    }
}
