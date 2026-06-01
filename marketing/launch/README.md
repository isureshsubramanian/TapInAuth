# Launch marketing assets

This directory holds everything needed to announce TapInAuth v0.5.0 across the channels you care about.

## What's here

| File                      | Purpose                                                  |
| ------------------------- | -------------------------------------------------------- |
| `twitter-thread.md`       | 7-tweet thread for X/Twitter                             |
| `linkedin-post.md`        | Single long-form post for LinkedIn                       |
| `reddit-r-dotnet.md`      | Title + body for `/r/dotnet`                             |
| `hacker-news.md`          | Show HN title, URL, and first-comment body               |
| `blog-announcement.md`    | Long-form announcement for dev.to / Medium / personal blog |
| `package-blurbs.md`       | NuGet `<Description>` text for every shipped package     |
| `og-card.svg` + `.png`    | Open Graph card (1200x630) for the tapinauth.io homepage |
| `hero-banner.svg` + `.png`| Hero banner (1600x500) for README and homepage           |

## Suggested launch sequence

1. **T-1 day** — push final code, tag `v0.5.0`, run the Release workflow against a personal NuGet account first to dry-run the publish.
2. **Launch morning (≈ 9am PT)** — post the Twitter thread, LinkedIn post, blog announcement, all roughly simultaneously. Quote-tweet your own blog post from the dev account.
3. **Launch +2 hr** — submit Show HN. The 9–11am PT window historically has the best HN traction; later posts often die on `new`.
4. **Launch afternoon** — post in /r/dotnet. Match the title + body from `reddit-r-dotnet.md` exactly; mods value technical depth.
5. **Day 2+** — DM Anthropic-the-mailing-lists (`dotnet-weekly`, `dotnetkicks`), .NET podcasters, and the .NET Foundation contact for incubation conversation.

## Hashtag / linking discipline

- Use **only one hashtag block**, at the *end* of social posts. Inline hashtags in the body read spammy.
- Always link to `tapinauth.io` (which 301s to wherever). It keeps social-card previews consistent and gives us one place to swap CTAs later.
- For HN: don't ask for upvotes in the post or anywhere else. Mods can kill the submission for it.
