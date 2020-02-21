---
title: Steve Dower | Musings and Mutterings
permalink: /
is_root: true
---


## My Pages

<img src="/assets/me.128x128.jpg" alt="Me" class="alignright" />

**[About Me](/about)** - More than you need to know

**[Speaking](/speaking)** - My talk history and recordings

**[Research](/research)** - My (past) research and publications

**[@zooba](https://twitter.com/zooba)** - My Twitter stream of consciousness

## Latest Post: {{ site.posts.first.title }}

{{ site.posts.first.excerpt }}

[Continue reading...]({{ site.posts.first.url }})

## My Posts

{% for post in site.posts %}
**[{{ post.date | date_to_string }}]({{post.url}})** - {{post.title}}
{% endfor %}
