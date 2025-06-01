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
