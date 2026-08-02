# Space Engineers Script Checker

This is a small utilitiy checks your in-game scripts for common issues.
Your scripts can be written exactly like you see it in-game, so no need
for getting dependencies or adding using or mucking around with
templates.

Currently only tested on linux, *should* work on windows.

Unlike the awesom [Malware's devkit for SE (MDK²-SE)](https://github.com/malforge/mdk2) tools. This is a very basic tool for smashing out a quick and dirty script or vibe coding your way around a survival game. It's lacking some of the features provided by MDK2.

sesc does not enforce the whitelist used by Space Engineers and doesn't warn you about the character limits. Feel free to make a PR.

Report bugs and any in-game errors this tool didn't catch, prehaps one day they'll be sorted.

## Installation
- Clone the repo.
- CD into the repoo.
- Install dotnet tools.
- run `donnet build`
- probably add the binary to your PATH or symlink it to `~/.local/bin/sesc`

## Usage

1. Create a file called hello-world.cs and write some code.
```cs
public void Main()
{
    Echo("Hello, world!");
}
```
2. run `sesc hello-world.cs`

### More Usage
Usage: sesc [options] script_file
Options:
  -e  --editor    Format the output suitable for editors and other tools.
                      (script_file:line:col_start:col_end:error_type:message)
  -h  --help      Get this help text.";

>> you can use **stdin**, you must still provide the `script_file` path to be included in the messages. This feature is intened to be used with `-e` for use with other tools, like nvim or with an LLM.

## Useful resources
https://malforge.github.io/spaceengineers/mdk2/
