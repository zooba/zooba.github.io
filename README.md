---
title: Steve Dower | Musings and Mutterings
permalink: /
is_root: true
---

<img src="/assets/me.128x128.jpg" alt="Me" class="alignright" />

## My Pages

**[About Me](/about)** - More than you need to know

**[Speaking](/speaking)** - My talk history and recordings

**[Research](/research)** - My (past) research and publications


## Latest Post: {{ site.posts.first.title }}

{{ site.posts.first.excerpt }}

[Continue reading...]({{ site.posts.first.url }})

## My Posts

{% for post in site.posts %}
**[{{ post.date | date_to_string }}]({{post.url}})** - {{post.title}}
{% endfor %}
