using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace GlobToRegex
{
	public class ExclusionRules
	{
		public string ConvertRulesToRegexPattern(IEnumerable<string> globRules, string rootPath)
		{
			// NOTES:
			// Given a set of input rules, this returns a single regex pattern that covers all of those rules.

			// rootPath: the input rules are relative to this rootPath, but the generated regex will only match strings that explicitely include the rootPath (the rootPath is included in the generated pattern).
			// For example, if...
			//   rootPath = "C:\Projects\MyFiles\"
			//   rules[]:  "a\b\c", "example.htm"
			// ...then the resulting regex will match:
			//   "C:\Projects\MyFiles\a\b\c" and "C:\Projects\MyFiles\example.htm"
			// but will NOT match: "C:\Projects\Other Folder\example.htm"

			// The input rules are passed as a series of strings (each being a line from the ignore.txt file)

			// Each rule should be the path of a file or directory.
			// Rules are considered relative to rootPath, regardless of if they begin with a slash (i.e. '/a/b/c' will be treated like 'a/b/c').

			// If a rule is for a directory, it must end with a trailing slash (e.g. 'a/b/c/')
			// If a rule is for a file, ist must NOT end with a trailing slash (e.g. 'a/b/c')

			// If a rule begins with an exclamation point, it's an exception to the rules (e.g. if there's a rule "a/b" and another rule "!a/b/c/d", then the "a/b" folder is ignored EXCEPT the "a/b/c/d" folder is NOT ignored).
			// Order is not important (exceptions can occur before or after the rule(s) they are an exception for).

			// Rules only support literals, no wildcards (maybe they'll be added in the future)

			// Rules can include comments (e.g. 'a/b/c/  # this is the c folder')
			// Leading and trailing whitespace is ignored.
			// Empty rules are ignored (except an exclamation point without anything else seems like an obvious mistake so an InvalidOperationException will be thrown).

			// Slashes and backslashes are considered equivalent, and the regex will match either.
			// File and directory names are considered case-insensitive (additional note: the pattern does not include any regex-options embedded into it, so we're assuming that the caller will use the appropriate case-insensitive option when using the pattern)

			// Even though the input rules must differentiate between files and directories, this is an artifact of our use-case where the rules file exists as part of another component (which requires that distinction).  We're using it here for optimizing the regex a little bit (for files we don't include the patterns for matching children).  This is not considered a requirement with regards to the regex pattern that gets generated, so we do not guarantee that "a/b/c/" won't match a file named "c" (in fact, it will!) nor that "a/b/c" won't match a directory (it will, but there could be inconsistencies with regards to matching sub-files and sub-directories -- this scenerio is currently untested).

			// TODO: Consider removing the distinction between files and directories and just treat them all as directories for the sake of the regex.

			// Duplicate rules are not currently filtered (they'll cause duplicates in the generated regex).

			// TODO: Filter duplicate rules.
			// TODO: Filter rules that are completely covered by other rules (e.g. if there's a rule "a/b/c/", then a rule of "a/b/c/d/e/f/" can be considered a duplicate)

			// Returns an empty string if there are no rules to be considered.

			// Directory rules may match files, but file rules may not match directories. The rules provided as input must differentiate between files and directories (directories must have a trailing slash), but the resulting rules are not required to differentiate between them--in other words, if a rule says "ignore the directory 'a/b/c/', the resulting rules may match "a/b/c" even if 'c' is a file).  However, if a rule says "ignore the FILE '

			// TODO: make regex work with either type of slash instead of needing to normalize

			var excludeDirRules = new List<string>();
			var excludeFileRules = new List<string>();
			var includeDirRules = new List<string>();
			var includeFileRules = new List<string>();

			int ruleNumber = 0;
			foreach (string iRule in globRules)
			{
				ruleNumber++;
				string s = iRule;

				// anything after a '#' is a comment
				int x = s.IndexOf('#');
				if (x > -1)
					s = s.Substring(0, x);

				s = s.Trim();
				if (s.Length == 0)
					continue;

				bool isNegated = false;
				if (s[0] == '!')
				{
					isNegated = true;
					s = s.Substring(1);
				}

				// normalize slashes (we're choosing forward slashes because they don't need to be escaped in regex patterns, making it easier to search & replace them with a "[\\/]" pattern to match either type)
				s = s.Replace("\\", "/");

				// if it has a leading slash, get rid of it
				if (s[0] == '/')
					s = s.Substring(1);

				// if it has a trailing slash, make a note that it's a directory and remove the slash
				bool isDir = false;
				int lastPos = s.Length - 1;
				if (s[lastPos] == '/')
				{
					isDir = true;
					s = s.Substring(0, lastPos);
				}

				if (isNegated)
				{
					if (string.IsNullOrEmpty(s))
						throw new InvalidOperationException($"A rule cannot be an empty exception (#{ruleNumber}: '{iRule}').");
					{
						if (isDir)
							includeDirRules.Add(s);
						else
							includeFileRules.Add(s);
					}
				}
				else
				{
					if (isDir)
						excludeDirRules.Add(s);
					else
						excludeFileRules.Add(s);
				}
			}

			if (!excludeDirRules.Any() && !excludeFileRules.Any())
				return string.Empty;

			var regexPatterns = new List<string>();

			// Our regex pattern will always begin with: "^" + escaped_rootPath + "(?:" + (each rule joined together with "|") + ")"
			//      ^ = must match from start of string
			//      escaped_rootPath = exact literal match to the root path (so anything not in this path will never match)
			//      (?: = begin a non-capturing group
			//        ...each rule pattern joined together with "|"... = match one of these rules
			//      )   = end group
			// (the grouping is so that the "|" isn't applied to the ("^" + escaped_rootPath)

			// For a rule that ignores a directory with no exceptions (e.g. 'a/b/c/"): "^" + escaped_path + "(?:$|[\\/])"
			//      ^ = must match from start of string
			//      escaped_path = exact literal match to the path
			//      (?:$|[\\/]) = match either a slash OR end of string (i.e. match "a/b/c" or "a/b/c/d", but not "a/b/cat" or "a/b/cat/file")

			// For a rule that ignores a directory with exceptions:
			//  Example:  "a/b/c/"  & "!a/b/c/subdir/include-me/"  &  "!a/b/c/sub/dir/also-me.txt"
			//  We want to include "a/b/c" and everything within it EXCEPT for those exceptions...
			// TODO: write explanation of how this regex works and is built.

			// For rules that ignore files (e.g. 'a/b/c.htm'):  "^" + escaped_path + "$"
			//      ^ = must match from start of string
			//      escaped_path = exact literal match to the path
			//      $ = must match end of string (so won't match sub-files or sub-dirs)

			// "" vs @"":  the two different types of string literals are used here for the purposes of readability (my IDE shows them in different colors). I'm using @"" for regex pieces, and "" for non-regex.  This is just for source readability (my IDE shows them in different colors).
			// Keep in mind that "\\" and @"\\" are not the same... "\\" actually means a single slash (C#-escaped), and @"\\" means two slashes (which is a regex-escaped version of a single literal slash).

			foreach (string i in excludeDirRules)
			{
				IList<string> includeDirExceptions = includeDirRules.Where(xInclude => xInclude.StartsWith(i + "/", StringComparison.OrdinalIgnoreCase)).ToList();
				IList<string> includeFileExceptions = includeFileRules.Where(xInclude => xInclude.StartsWith(i + "/", StringComparison.OrdinalIgnoreCase)).ToList();

				if (includeDirExceptions.Any() || includeFileExceptions.Any())
				{
					IList<string> exceptionPatterns = new List<string>();
					foreach (string iException in includeDirExceptions)
					{
						string exceptionPart = iException.Substring(i.Length);
						if (!string.IsNullOrEmpty(exceptionPart))
						{
							// each ancestor directory in the path needs to be an exception
							string s = Path.GetDirectoryName(exceptionPart);
							while (!string.IsNullOrEmpty(s) && !string.Equals(s, "/", StringComparison.Ordinal))
							{
								exceptionPatterns.Add(@"(?:" + Regex.Escape(s).Replace("/", @"[\\/]") + @"[\\/]?$)");
								s = Path.GetDirectoryName(s);
							}

							// directory exceptions could either have nothing after, or have a slash
							string pattern = @"(?:" + Regex.Escape(exceptionPart).Replace("/", @"[\\/]") + @"(?:$|[\\/]))";
							exceptionPatterns.Add(pattern);
						}
					}
					foreach (string iException in includeFileExceptions)
					{
						string exceptionPart = iException.Substring(i.Length);
						if (!string.IsNullOrEmpty(exceptionPart))
						{
							// each ancestor directory in the path needs to be an exception
							string s = Path.GetDirectoryName(exceptionPart);
							while (!string.IsNullOrEmpty(s) && !string.Equals(s, "/", StringComparison.Ordinal))
							{
								exceptionPatterns.Add(@"(?:" + Regex.Escape(s).Replace("/", @"[\\/]") + @"[\\/]?$)");
								s = Path.GetDirectoryName(s);
							}

							// file exceptions shouldn't have anything else at the end
							string pattern = Regex.Escape(exceptionPart).Replace("/", @"[\\/]") + @"$";
							exceptionPatterns.Add(pattern);
						}
					}
					string exceptionPattern = Regex.Escape(i).Replace("/", @"[\\/]") + @"(?!" + string.Join(@"|", exceptionPatterns) + @")[\\/].+";
					regexPatterns.Add(exceptionPattern);
				}
				else
				{
					string pattern = Regex.Escape(i).Replace("/", @"[\\/]") + @"(?:$|[\\/])";
					regexPatterns.Add(pattern);
				}
			}

			foreach (string i in excludeFileRules)
			{
				string pattern = Regex.Escape(i).Replace("/", @"[\\/]") + @"$";
				regexPatterns.Add(pattern);
			}

			var masterRule = new StringBuilder();

			masterRule.Append(@"^");

			string rootPathEscaped = Regex.Escape(rootPath.Replace("\\", "/")).Replace("/", @"[\\/]");
			masterRule.Append(rootPathEscaped);

			if (regexPatterns.Count == 1)
				masterRule.Append(regexPatterns.First());
			else
			{
				masterRule.Append(@"(?:");
				int c = 0;
				foreach (string i in regexPatterns)
				{
					if (c++ > 0)
						masterRule.Append(@"|");
					masterRule.Append(@"(?:");
					masterRule.Append(i);
					masterRule.Append(@")");
				}
				masterRule.Append(@")");
			}

			return masterRule.ToString();
		}
	}
}