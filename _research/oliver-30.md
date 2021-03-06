---
layout: page
title: Oliver 30 TSP
description: 'Details of a commonly used instance of the [travelling salesman problem](https://en.wikipedia.org/wiki/Travelling_salesman_problem) that are not widely available online.'
slug: oliver-30
redirect_from: /blog/research/oliver-30
sort_key: Z
---

Oliver30 is a commonly used benchmark for the Travelling Salesman Problem (TSP). However, before this page, the city coordinates making up Oliver30 were not easily available online. Those that are available are quite often wrong (including my own technical report for a while). I have spent a long time trying to find the correct coordinates and as far as I can tell they are not online.

These coordinates come from I. M. Oliver, D. J. Smith and J. R. C. Holland, "A study of permutation crossover operators on the travelling salesman problem," in _Proceedings of the Second International Conference on Genetic Algorithms on Genetic algorithms and their application_, 1987, pp. 224-230, who derived it from diagrams in an earlier work by [J. J. Hopfield and D. W. Tank](https://scholar.google.com/scholar?q=neural+computation+of+decisions+in+optimization+problems). Oliver et al. is usually cited as the source of the Oliver30 TSP.

These coordinates are included in `esec` as a plain-text file. They are listed here in the numerical order in which they are normally labelled by M. Dorigo, which is different to the order they were presented by Oliver et al. and is not the shortest path.

```
54, 67
54, 62
37, 84
41, 94
 2, 99
 7, 64
25, 62
22, 60
18, 54
 4, 50
13, 40
18, 40
24, 42
25, 38
44, 35
41, 26
45, 21
58, 35
62, 32
82,  7
91, 38
83, 46
71, 44
64, 60
68, 58
83, 69
87, 76
74, 78
71, 71
58, 69
```

The shortest cycle length is 423.741 when using the exact distance between each city. If each distance is rounded to the nearest integer, the shortest cycle length is 420. City numbers are 1-based indices into the list above. Extra spaces are included to emphasise those sections of the path that are not sequential indices.

```
1  3 4 5 6 7 8 9 10 11 12 13 14 15 16 17 18 19 20 21 22 23  25 24  26 27 28 29 30  2
```
