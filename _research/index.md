---
layout: page
title: Research
permalink: /research
redirect_from: /blog/research
---

Pages in this section:
<dl>
{% assign research = site.research | sort:"sort_key" %}
{% for p in research %}
{% if p.url != page.url %}
<dt><a href="{{ p.url }}">{{ p.title }}</a></dt>
<dd>{{ p.description | markdownify }}</dd>
{% endif %}
{% endfor %}
<dt><a href="/research/esdlgrammar">ESDL Grammar</a></dt>
<dd>A BNF-style grammar for ESDL.</dd>
</dl>