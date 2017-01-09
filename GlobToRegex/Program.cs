using System;
using System.IO;
using System.Text.RegularExpressions;

namespace GlobToRegex
{
	class Program
	{
		static void Main(string[] args)
		{
			// nested exclude/include scenerio:
			//   exclude:  a/b/c/
			//   include:  a/b/c/d/e/f/
			//   exclude:  a/b/c/d/e/f/g/h/
			//   include:  a/b/c/d/e/f/g/h/i/j/

			// should be excluded:
			//   a/b/c/file1.htm
			//   a/b/c/d/file2.htm
			//   a/b/c/d/e/file3.htm
			//   a/b/c/d/x/file4.htm
			//   a/b/c/d/e/f/g/h/file8.htm
			//   a/b/c/d/e/f/g/h/z/file9.htm
			//   a/b/c/d/e/f/g/h/i/file10.htm

			// should be included:
			//   a/b/c/d/e/f/file5.htm
			//   a/b/c/d/e/f/y/file6.htm
			//   a/b/c/d/e/f/g/file7.htm
			//   a/b/c/d/e/f/g/h/i/j/file11.htm
			//   a/b/c/d/e/f/g/h/i/j/file12.htm
			//   a/b/c/d/e/f/g/h/i/j/k/l/m/file13.htm

			// globRules = the lines from the ignore.txt file...
			var globRules = new string[]
			{
				"App_Data/  # exclude the entire App_Data folder",
				"!App_Data/config/global/  # except the global config folder (and everything in it)",
				"!App_Data/config/live/general.json  # and except this single file",
				"assets/  # exclude the folder where user-uploaded content goes",
				"test/subdir1/subdir2/",
				"test/subdir1/subdir2/file1",
				"test/subdir1/subdir2/file2",
				"a/b/c/",
				"!a/b/c/d/e/f/",
				"a/b/c/d/e/f/g/h/",
				"!a/b/c/d/e/f/g/h/i/j/"
			};

			// these tests should match rules and be considered as "excluded"
			var sampleExcludeTests = new string[]
			{
				"App_Data/abc.txt",
				"App_Data/.gitignore",
				"app_data/placeholder.txt",
				"App_Data/config/live/developer.json",
				"App_Data/config/live/email.json",
				"App_Data/config/live/google.json",
				"App_Data/config/live/http.json",
				"App_Data/config/local/developer.json",
				"App_Data/config/local/email.json",
				"App_Data/config/local/general.json",
				"App_Data/config/local/google.json",
				"App_Data/config/local/http.json",
				"App_Data/config/staging/developer.json",
				"App_Data/config/staging/email.json",
				"App_Data/config/staging/general.json",
				"App_Data/config/staging/google.json",
				"App_Data/config/staging/http.json",
				"App_Data/email/email1.abc.txt",
				"App_Data/email/email2.txt",
				"App_Data/data.txt",
				"App_Data/data.gz",
				"App_Data/logs/log-123.json",
				"assets/docs/placeholder.txt",
				"assets/images/placeholder.txt",
				"assets/images/subpage/logo.png",
				"assets/pdf/placeholder.txt",
				"a/b/c/file1.htm",
				"a/b/c/d/file2.htm",
				"a/b/c/d/e/file3.htm",
				"a/b/c/d/x/file4.htm",
				"a/b/c/d/e/f/g/h/file8.htm",
				"a/b/c/d/e/f/g/h/z/file9.htm",
				"a/b/c/d/e/f/g/h/i/file10.htm"
			};

			// these tests should either NOT match any of the rules, or match negative (!) rules so they should NOT be considered as excluded...
			var sampleIncludeTests = new string[]
			{
				"App_Data/config/global/developer.json",
				"App_Data/config/global/email.json",
				"App_Data/config/global/general.json",
				"App_Data/config/global/google.json",
				"App_Data/config/global/http.json",
				"App_Data/config/live/general.json",
				"web.config",
				"gruntfile.js",
				"Views/general.master",
				"Views/PageTemplates/HomePage.master",
				"Views/PageTemplates/SubPage.master",
				"Views/PageTemplates/Widgets/test.ascx",
				"a/b/c/d/e/f/file5.htm",
				"a/b/c/d/e/f/y/file6.htm",
				"a/b/c/d/e/f/g/file7.htm",
				"a/b/c/d/e/f/g/h/i/j/file11.htm",
				"a/b/c/d/e/f/g/h/i/j/file12.htm",
				"a/b/c/d/e/f/g/h/i/j/k/l/m/file13.htm"
			};

			// MSDeploy package manifests have a common path in front of all of the file specs, so here's a random one just to make sure the patterns work with it...
			const string rootPath = @"C:\Projects\MyProject\BaseDirectory\";

			var converter = new ExclusionRules();
			string regexPattern = converter.ConvertRulesToRegexPattern(globRules, rootPath);

			Console.WriteLine($"Regex pattern: {regexPattern}");

			foreach (string i in sampleExcludeTests)
			{
				bool isExcluded = TestIfFileExcluded(rootPath + i, regexPattern);
				Console.Write(!isExcluded ? "** FAILED **" : "passed");
				Console.WriteLine($" exclusion: {i}");
			}
			foreach (string i in sampleIncludeTests)
			{
				bool isExcluded = TestIfFileExcluded(rootPath + i, regexPattern);
				Console.Write(isExcluded ? "** FAILED **" : "passed");
				Console.WriteLine($" inclusion: {i}");
			}

			Console.WriteLine("*** done ***");
			Console.ReadLine();
		}

		static bool TestIfFileExcluded(string filePath, string regex)
		{
			//filePath = filePath.Replace("/", "\\");

			if (!string.IsNullOrEmpty(regex))
			{
				// to simulate how msdeploy handles skip rules--if a directory is skipped, then everything within it is skipped (it'll never reach the point where it tested files or sub-dirs)
				string dirPath = filePath;
				while (!string.IsNullOrEmpty(dirPath))
				{
					dirPath = Path.GetDirectoryName(dirPath);

					if (!string.IsNullOrEmpty(dirPath) && Regex.IsMatch(dirPath, regex, RegexOptions.IgnoreCase))
						return true;
				}

				if (Regex.IsMatch(filePath, regex, RegexOptions.IgnoreCase))
					return true;
			}

			return false;
		}
	}
}
