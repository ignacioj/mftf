Searcher and file copy utility using the $MFT.

The tool can parse the $MFT from a live system, from a mounted (read-only
included) logical drive or from a copy of the $MFT.

It can search for folders, files and ADS,s by parsing the $MFT. It shows the
sizes and SI and FN datetimes.

It can copy files or ADS,s using the references provided in the results.
The copy is made by reading the data from the clusters so that you can copy
protected system files or files in use.

Main options:

 -d drive_letter               $MFT from this logical unit.
 
 -o file                       $MFT file.
 
 -h                            This help.
 
 
String search:

 -f "string1[|string2 with spaces|string3?...]"
 
 -f "folder\string"
 
                          The results will be filtered by the folder name.
                          
                          The match is always case insensitive.
                          
                          " as delimiters for the whole group of strings.
                          
                          | is the separator.
                          
                          ? at the end of any string for an exact coincidence.
                          
 -ff file.txt      The strings to search for are in file.txt. One string per line. You can use ?.
                    
 -fr string        Search in the 1024 bytes of the MFT record.
 
==== Directory based search. Use always \\\\ for the root:

 -fd "\\\\Dir1\dir2"              It will match any directories dir2...
 
 -fd "\\\\Dir1\dir2?"             Can use ? with the last directory.
 
 -r N                            Recursion level (number). Default is 0.
 
==== ADS,s search

 -fads            Find all the ADSs in the logical unit.
 
======== Can be used with any of the previous search options:

 -x               Save the results in a file in order to use the option -c.
 
 -t               Display the results in timeline format.
 
 -s               Display only the file name.
 
==== Information options:

 -i record_number      Show information of the MFT record.
 
 -w record_number      Write on screen the 1024 bytes of the MFT record.
 
==== Copy options:

 -cr record_number     Copy the 1024 bytes of the MFT record to this folder.
 
======== For live systems or logical units:

 -c "ref1[|ref2..]"  Copy the referenced file/s to this folder.
 
                                     Use | as separator.
                                     
 -c list.txt           Copy all the files referenced in the file list.txt.
 
                        Each line MUST start with: reference + [TAB].
                        
Examples:

mftf.exe -d e: -f "svchost|mvui.dll|string with spaces|exact match?"

mftf.exe -d e -c 4292:128-1

