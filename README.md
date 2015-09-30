The tool can parse the $MFT from a live system, from a mounted (read-only included) logical drive or from a copy of the $MFT.

Deleted files and folders have their path with the prefix "?".

It can copy files or ADS,s using the references provided in the results.

The copy is made by reading the data from the clusters so that you can copy protected system files or files in use.
(Imports from "kernel32.dll":	CloseHandle, CreateFile, ReadFile, SetFilePointerEx).


==== Main options:

 -d drive_letter               Search/copy files from this logical unit.
 
 -o file                       Search files from this offline $MFT file.
 
 -h                            This help.
 
==== Timeline of the MFT:

 -tl
 
==== Logical string search:

 -f "string1|string2 with spaces|string3<"
 
 -f "folder\string"
 
                          The results are filtered using the string "folder".
						  
                          The match is always case insensitive.
						  
                          " as delimiters for the whole group of strings.
						  
                          | is the separator.
						  
                          < at the end of "string" for an exact coincidence.
						  
 -ff file.txt      The strings to search for are in file.txt. One string per line. Can use <.
 
==== Raw search

 -fr string        Search in the 1024 bytes of each MFT record.
 
==== Root based search: files and folders under the tree

 -fd "\\\\Dir1\dir2"             It will match any directories like dir2...
 
 -fd "\\\\Dir1\dir2\Dir3<"       Can use < with the last directory.
 
 -r N                            Recursion level (number). Default is 0.
 
==== ADS,s search

 -fads            Find all the ADSs in the logical unit.
 
======== Can be used with any of the previous search options:

 -x               Save the results in a file in order to use the option -c.
 
 -t               Display the results in a timeline format.
 
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

	In this example the file has 4 $FN attributes and two ADS and the Attribute List points to another record.

mftf -d c -i 623677

Record: 623677 [Attribute List points to records numbers: 623745]

[File]  \\\\_SMSVC~1.INI

[File]  \\\\_SMSvcHostPerfCounters_D.ini

[File]  \\\\_SMSvcHostPerfCounters_D.ini

[File]  \\\\_SMSvcHostPerfCounters_D.ini

	SI[MACB]: 2014/02/18 07:51:58.3286194   2014/02/18 07:51:58.3286194   2014/08/23 12:01:53.1659607   2014/02/18 07:51:58.3286194

FN[MACB]: 2014/02/18 07:51:58.3286194   2014/02/18 07:51:58.3286194   2014/02/18 07:51:58.3286194   2014/02/18 07:51:58.3286194

FN[MACB]: 2014/02/18 07:51:58.3286194   2014/02/18 07:51:58.3286194   2014/02/18 07:51:58.3286194   2014/02/18 07:51:58.3286194

FN[MACB]: 2014/02/18 07:51:58.3286194   2014/02/18 07:51:58.3286194   2014/02/18 07:52:00.2179972   2014/02/18 07:51:58.3286194

FN[MACB]: 2014/02/18 07:51:58.3286194   2014/02/18 07:51:58.3286194   2014/08/23 12:01:53.1503598   2014/02/18 07:51:58.3286194

Reference: 623677:128-1 [Size: 41 bytes|| Size on disk: 0 bytes]

[ADS] Name: hmx33t [Reference: 623677:128-2 || Size: 1069547520 bytes]

[ADS] Name: Zone.Identifier [Reference: 623677:128-3 || Size: 23 bytes]


	The same file in the timeline format with dates and times from all the $FN attributes.
	The dates and times of the ADS are those of the $SI attribute.

mftf -d c -f "_SMSvcHostPerfCounters_D" -t

Filetime,[MACB],filename,record,size

2014/02/18 07:51:58.3286194,SI[MA.B],\\\\_SMSvcHostPerfCounters_D.ini,623677,41

2014/08/23 12:01:53.1659607,SI[..C.],\\\\_SMSvcHostPerfCounters_D.ini,623677,41

2014/02/18 07:51:58.3286194,FN[MACB],\\\\_SMSvcHostPerfCounters_D.ini,623677,41

2014/02/18 07:51:58.3286194,FN[MA.B],\\\\_SMSvcHostPerfCounters_D.ini,623677,41

2014/02/18 07:52:00.2179972,FN[..C.],\\\\_SMSvcHostPerfCounters_D.ini,623677,41

2014/08/23 12:01:53.1503598,FN[..C.],\\\\_SMSvcHostPerfCounters_D.ini,623677,41

2014/02/18 07:51:58.3286194,SI[MA.B],\\\\_SMSvcHostPerfCounters_D.ini:hmx33t,623677,1069547520

2014/08/23 12:01:53.1659607,SI[..C.],\\\\_SMSvcHostPerfCounters_D.ini:hmx33t,623677,1069547520

2014/02/18 07:51:58.3286194,SI[MA.B],\\\\_SMSvcHostPerfCounters_D.ini:Zone.Identifier,623677,23

2014/08/23 12:01:53.1659607,SI[..C.],\\\\_SMSvcHostPerfCounters_D.ini:Zone.Identifier,623677,23

