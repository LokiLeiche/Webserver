[![Build](https://github.com/LokiLeiche/Webserver/actions/workflows/build.yml/badge.svg)](https://github.com/LokiLeiche/Webserver/actions/workflows/build.yml)
[![Tests](https://github.com/LokiLeiche/Webserver/actions/workflows/test.yml/badge.svg)](https://github.com/LokiLeiche/Webserver/actions/workflows/test.yml)

# What is this?
For learning and fun purposes, I decided to make my own little Webserver program. It's my second project in C#, therefore I'm still learning and keep improving. It's also worth noting that I'm not following any tutorial or looking at another project for inspiration, I'm doing things from scratch kinda as freestyle however I feel like is right and what works for me. I'm using the [HTTP Semantics](https://www.rfc-editor.org/rfc/rfc9110.html#name-identifiers-in-http) as reference though.

# Does this conform with the official [HTTP semantics](https://www.rfc-editor.org/rfc/rfc9110.html#name-identifiers-in-http)?
Currently, no. Not entirely. Since I'm doing this from scratch, I'm also working through the semantics as reference but the semantics are over 60k words long and I'm far away from being done reading it and more importantly, implementing it in code. At this point simply serving files as requested by the client works, but lots of headers are still ignored and behaviour at some places might not be as expected. I'm working on it and will keep improving things though.

# Should you use this?
Probably not. This project is still work in progress and even when it is done one day, it will never reach the functionality, reliability and security of other popular webserver programs like apache2. If you just want to host your website, use something else. If you want to use this to experiment, learn from it or maybe even contribute, feel free to do so. Keep in mind that reliability and security issues are to be expected, use in a controlled environment.

# Can you contribute?
If you really want to contribute to this project and show me where I could improve things, feel free to open an issue or a PR!

# How to use
You have to build this from source yourself, I will not be providing any prebuilt releases since I can not sign them and your OS will scream at you like this is malware. By building this yourself it also becomes your responsibility to check the source code for anything that could harm your system in any way and you acknowledge the risk that come with using this program. After building everything, edit the config.json to your needs and add your Websites files in the Websites directory. I recommend following the same pattern as provided with the example.com domain. Remember to remove or disbale the example.

# Build instructions
## Windows
1. Install .NET 8.0 using Visual Studio: https://learn.microsoft.com/de-de/dotnet/core/install/windows#install-with-visual-studio
2. Open a terminal (cmd/powershell)
3. Clone the repo: `git clone https://github.com/LokiLeiche/Webserver`
EITHER:
4. Move into the new directory: `cd Webserver`
5. Build the code: `dotnet build --configuration Release`
OR:
4. Open the solution in Visual Studio
5. Build in Visual Studio

The built files are now located under Webserver/bin/Release/net8.0/, from there you can run the Webserver.exe file.


## Linux
1. Install .NET 8.0 for your linux distribution, probably available from your package manager. As example for ubuntu: `sudo apt update && sudo apt install dotnet8`
2. Clone the repo: `git clone https://github.com/LokiLeiche/Webserver`
3. CD into new directory: `cd Webserver`
4. Build the code: `dotnet build --configuration Release`
The built files are now located under Webserver/bin/Release/net8.0/, from there you can run the file "Webserver". You can also move the files to any other directory wherever you want them to be.



# PHP support
This webserver supports PHP. As a little disclaimer tho, the implementation is not perfect yet. It works fine but it's not suitable for larger scale applications with lots of traffic since there's currently no FastCGI implementation. If you still wish to use PHP, follow these steps.

## Windows
1. Download your desired PHP version as .zip from https://windows.php.net/download
2. Extract the contents into any directory that you can remember. For example C:\php\your_version\
3. Type Environment into your search bar and it should show something like environment variables, click that. A new window will pop up.
4. In that new window, press environment variables
5. Under environment variables in the bottom half of the window search for PATH and double click it
6. Create a new entry and paste the path where you just installed your PHP

## Linux
For linux you just need to download php-cgi, your distribution probably has a package for it to install via package manager. As example for ubuntu:
1. sudo apt update
2. sudo apt install php-cgi
And that's it, you should now be able to run php files from linux.
