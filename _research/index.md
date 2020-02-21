---
layout: page
title: Research
permalink: /research
redirect_from: /blog/research
---

**Feb 2020** - This content is mostly archival, as I am no longer working in this area. I have updated links where needed, but the text remains as it did in 2012 (revealing all my now-dashed hopes and dreams). I hope it is interesting to someone, and I _am_ still happy to chat about it.

Pages in this section:
<dl>
{% assign research = site.research | sort:"sort_key" %}
{% for p in research %}
{% if p.url != page.url %}
<dt><a href="{{ p.url }}">{{ p.title }}</a></dt>
<dd>{{ p.description | markdownify }}</dd>
{% endif %}
{% endfor %}
</dl>