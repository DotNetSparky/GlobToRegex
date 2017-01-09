# GlobToRegex

*This is a work in progress / proof-of-concept...*
Given a set of input rules, this returns a single regex pattern that covers all of those rules. (these rules are intended to be read from a text file, e.g. via File.ReadLines()--this file is similar to ".gitignore" but currently without support for any wildcards.

## Background
This is a test project I used to write the regex-generating code, which is going to be used in other projects.  My use-case that this is for:  I have msbuild project files that use msdeploy tasks to perform site deployments, backups, etc.  My projects already contain a file (similar to .gitignore) that identifies folders and files that might exist in the production sites that aren't part of my projects (most are unfortunate junk that someone else has up there that I can't touch), that I want ignored as if they don't exist during all deployment operations (I don't want to be overwritten or deleted).  I use msdeploy's skip-rules to do this... the skip rules match files and paths based on regular expressions, so I wanted to use that existing 'ignore file' to generate a regular expression that would work.

## Notes

rootPath: the input rules are relative to this rootPath, but the generated regex will only match strings that explicitely include the rootPath (the rootPath is included in the generated pattern).

For example, if...
* rootPath = "C:\Projects\MyFiles\"
* rules[]:  "a\b\c", "example.htm"

...then the resulting regex will match:
* "C:\Projects\MyFiles\a\b\c"
* "C:\Projects\MyFiles\example.htm"

...but will NOT match: "C:\Projects\Other Folder\example.htm"

* The input rules are passed as a series of strings (each being a line from the ignore.txt file)
* Each rule should be the path of a file or directory.
* Rules are considered relative to rootPath, regardless of if they begin with a slash (i.e. '/a/b/c' will be treated like 'a/b/c').
* If a rule is for a directory, it must end with a trailing slash (e.g. 'a/b/c/')
* If a rule is for a file, ist must NOT end with a trailing slash (e.g. 'a/b/c')
* If a rule begins with an exclamation point, it's an exception to the rules (e.g. if there's a rule "a/b" and another rule "!a/b/c/d", then the "a/b" folder is ignored EXCEPT the "a/b/c/d" folder is NOT ignored).
* Order is not important (exceptions can occur before or after the rule(s) they are an exception for).
* Rules only support literals, no wildcards (maybe they'll be added in the future)
* Rules can include comments (e.g. 'a/b/c/  # this is the c folder')
* Leading and trailing whitespace is ignored.
* Empty rules are ignored (except an exclamation point without anything else seems like an obvious mistake so an InvalidOperationException will be thrown).
* Slashes and backslashes are considered equivalent, and the regex will match either.
* File and directory names are considered case-insensitive (additional note: the pattern does not include any regex-options embedded into it, so we're assuming that the caller will use the appropriate case-insensitive option when using the pattern)
* Even though the input rules must differentiate between files and directories, this is an artifact of our use-case where the rules file exists as part of another component (which requires that distinction).  We're using it here for optimizing the regex a little bit (for files we don't include the patterns for matching children).  This is not considered a requirement with regards to the regex pattern that gets generated, so we do not guarantee that "a/b/c/" won't match a file named "c" (in fact, it will!) nor that "a/b/c" won't match a directory (it will, but there could be inconsistencies with regards to matching sub-files and sub-directories -- this scenerio is currently untested).
* Duplicate rules are not currently filtered (they'll cause duplicates in the generated regex).
* Returns an empty string if there are no rules to be considered.

`ExclusionRule.cs` is the regex-generator.
`Program.cs` is a test console app that contains a series of test cases.  The program will print out the regex pattern that was generated, and the test results:

	Regex pattern: ^C:[\\/]Projects[\\/]MyProject[\\/]BaseDirectory[\\/](?:(?:App_Data(?!(?:\\config[\\/]?$)|(?:\\[\\/]?$)|(?:[\\/]config[\\/]global(?:$|[\\/]))|(?:\\config\\live[\\/]?$)|(?:\\config[\\/]?$)|(?:\\[\\/]?$)|[\\/]config[\\/]live[\\/]general\.json$)[\\/].+)|(?:assets(?:$|[\\/]))|(?:test[\\/]subdir1[\\/]subdir2(?:$|[\\/]))|(?:a[\\/]b[\\/]c(?!(?:\\d\\e[\\/]?$)|(?:\\d[\\/]?$)|(?:\\[\\/]?$)|(?:[\\/]d[\\/]e[\\/]f(?:$|[\\/]))|(?:\\d\\e\\f\\g\\h\\i[\\/]?$)|(?:\\d\\e\\f\\g\\h[\\/]?$)|(?:\\d\\e\\f\\g[\\/]?$)|(?:\\d\\e\\f[\\/]?$)|(?:\\d\\e[\\/]?$)|(?:\\d[\\/]?$)|(?:\\[\\/]?$)|(?:[\\/]d[\\/]e[\\/]f[\\/]g[\\/]h[\\/]i[\\/]j(?:$|[\\/])))[\\/].+)|(?:a[\\/]b[\\/]c[\\/]d[\\/]e[\\/]f[\\/]g[\\/]h(?!(?:\\i[\\/]?$)|(?:\\[\\/]?$)|(?:[\\/]i[\\/]j(?:$|[\\/])))[\\/].+)|(?:test[\\/]subdir1[\\/]subdir2[\\/]file1$)|(?:test[\\/]subdir1[\\/]subdir2[\\/]file2$))
	passed exclusion: App_Data/abc.txt
	passed exclusion: App_Data/.gitignore
	passed exclusion: app_data/placeholder.txt
	passed exclusion: App_Data/config/live/developer.json
	passed exclusion: App_Data/config/live/email.json
	passed exclusion: App_Data/config/live/google.json
	passed exclusion: App_Data/config/live/http.json
	passed exclusion: App_Data/config/local/developer.json
	passed exclusion: App_Data/config/local/email.json
	passed exclusion: App_Data/config/local/general.json
	passed exclusion: App_Data/config/local/google.json
	passed exclusion: App_Data/config/local/http.json
	passed exclusion: App_Data/config/staging/developer.json
	passed exclusion: App_Data/config/staging/email.json
	passed exclusion: App_Data/config/staging/general.json
	passed exclusion: App_Data/config/staging/google.json
	passed exclusion: App_Data/config/staging/http.json
	passed exclusion: App_Data/email/email1.abc.txt
	passed exclusion: App_Data/email/email2.txt
	passed exclusion: App_Data/data.txt
	passed exclusion: App_Data/data.gz
	passed exclusion: App_Data/logs/log-123.json
	passed exclusion: assets/docs/placeholder.txt
	passed exclusion: assets/images/placeholder.txt
	passed exclusion: assets/images/subpage/logo.png
	passed exclusion: assets/pdf/placeholder.txt
	passed exclusion: a/b/c/file1.htm
	passed exclusion: a/b/c/d/file2.htm
	passed exclusion: a/b/c/d/e/file3.htm
	passed exclusion: a/b/c/d/x/file4.htm
	passed exclusion: a/b/c/d/e/f/g/h/file8.htm
	passed exclusion: a/b/c/d/e/f/g/h/z/file9.htm
	passed exclusion: a/b/c/d/e/f/g/h/i/file10.htm
	passed inclusion: App_Data/config/global/developer.json
	passed inclusion: App_Data/config/global/email.json
	passed inclusion: App_Data/config/global/general.json
	passed inclusion: App_Data/config/global/google.json
	passed inclusion: App_Data/config/global/http.json
	passed inclusion: App_Data/config/live/general.json
	passed inclusion: web.config
	passed inclusion: gruntfile.js
	passed inclusion: Views/general.master
	passed inclusion: Views/PageTemplates/HomePage.master
	passed inclusion: Views/PageTemplates/SubPage.master
	passed inclusion: Views/PageTemplates/Widgets/test.ascx
	passed inclusion: a/b/c/d/e/f/file5.htm
	passed inclusion: a/b/c/d/e/f/y/file6.htm
	passed inclusion: a/b/c/d/e/f/g/file7.htm
	passed inclusion: a/b/c/d/e/f/g/h/i/j/file11.htm
	passed inclusion: a/b/c/d/e/f/g/h/i/j/file12.htm
	passed inclusion: a/b/c/d/e/f/g/h/i/j/k/l/m/file13.htm
	*** done ***
