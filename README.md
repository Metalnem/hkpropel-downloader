# HK Propel downloader

Download HK Propel (Human Kinetics) books in EPUB format. Read my blog post to learn more: [Reverse engineering yet another ebook format](https://mijailovic.net/2022/12/25/hkpropel/).

## Requirements

- [.NET 8.0](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
- [Google Chrome](https://www.google.com/chrome/)
- [Get cookies.txt LOCALLY](https://chromewebstore.google.com/detail/get-cookiestxt-locally/cclelndahbckbenkjhflpdbgdldlbecc)

## Usage

1) Open your HK Propel book in Chrome.
2) Extract the book ID from the URL.
3) Export HK Propel cookies in JSON format using `Get cookies.txt LOCALLY` extension.
4) Run `dotnet run --id <book_id> --cookies <cookies_path>`.

```
$ dotnet run
Description:
  Download HK Propel books in EPUB format.

Usage:
  hkpropel-downloader [options]

Options:
  --id <id> (REQUIRED)            HK Propel book ID.
  --cookies <cookies> (REQUIRED)  Path to exported Chrome cookies.
```