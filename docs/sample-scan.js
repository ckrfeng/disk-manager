// Interactive scan screen for the DiskManager page.
// The tree is made up and generic. Colours and ring layout follow the app's sunburst:
// each top-level folder gets its own hue from the app's palette, its contents are shades of that hue, and files are grey.
(function () {
  "use strict";

  // A made-up 487.34 GB system drive, with generic names. Sizes are in GB to two decimals;
  // leaves have a size and folders sum their children, so every total adds up.
  var TREE = {
    name: "Macintosh HD",
    children: [
      { name: "Users", children: [
        { name: "User 1", children: [
          { name: "Library", children: [
            { name: "Application Support", children: [
              { name: "Browser Data", children: [
                { name: "Profile 1", size: 6.41 },
                { name: "Profile 2", size: 4.87 },
                { name: "Cache", size: 2.92 }
              ] },
              { name: "Mail Store", size: 9.36 },
              { name: "Messages", size: 3.12 },
              { name: "Code Editor", children: [
                { name: "Extensions", size: 1.44 },
                { name: "Logs", size: 0.12 },
                { name: "Settings", size: 0.04 }
              ] }
            ] },
            { name: "Caches", children: [
              { name: "Browser", size: 3.1 },
              { name: "Media", size: 2.44 },
              { name: "Package Cache", size: 2.9 },
              { name: "System Logs", size: 0.31 }
            ] },
            { name: "Containers", children: [
              { name: "Notes", size: 0.82 },
              { name: "Photos Library", size: 3.41 }
            ] },
            { name: "Preferences", size: 0.22 },
            { name: "Logs", size: 0.18 }
          ] },
          { name: "Movies", children: [
            { name: "Home Videos", children: [
              { name: "Trip 2023", size: 21.04 },
              { name: "Trip 2024", size: 11.62 },
              { name: "Clips", size: 5.55 }
            ] },
            { name: "Exports", children: [
              { name: "Project A", size: 8.41 },
              { name: "Project B", size: 6.39 }
            ] }
          ] },
          { name: "Documents", children: [
            { name: "Work", children: [
              { name: "Reports", size: 6.21 },
              { name: "Spreadsheets", size: 4.07 },
              { name: "Presentations", size: 8.14 }
            ] },
            { name: "Scans", size: 6.03 },
            { name: "School", children: [
              { name: "Notes", size: 1.32 },
              { name: "Essays", size: 0.42 }
            ] },
            { name: "Archive 2022.zip", size: 6.29 }
          ] },
          { name: "Downloads", children: [
            { name: "Installers", children: [
              { name: "disk-image-1.dmg", size: 2.4 },
              { name: "disk-image-2.dmg", size: 4.4 }
            ] },
            { name: "Archives", children: [
              { name: "backup-old.zip", size: 4.1 },
              { name: "sources.tar", size: 3.51 }
            ] },
            { name: "Misc", children: [
              { name: "receipt.pdf", size: 0.03 },
              { name: "photos.zip", size: 2.58 }
            ] }
          ] },
          { name: "Music", children: [
            { name: "Library", children: [
              { name: "Album A", size: 2.11 },
              { name: "Album B", size: 1.94 },
              { name: "Album C", size: 1.15 }
            ] },
            { name: "Podcasts", size: 1.54 }
          ] },
          { name: "Pictures", children: [
            { name: "2023", size: 1.2 },
            { name: "2024", size: 1.46 },
            { name: "Screenshots", size: 0.6 }
          ] },
          { name: "Developer", children: [
            { name: "Xcode DerivedData", children: [
              { name: "Build A", size: 1.21 },
              { name: "Build B", size: 2.31 }
            ] },
            { name: "Simulators", children: [
              { name: "iOS 17", size: 1.02 },
              { name: "iOS 18", size: 1.04 }
            ] },
            { name: "Node modules", children: [
              { name: "project-a", size: 1.3 },
              { name: "project-b", size: 1.1 }
            ] },
            { name: ".cache", size: 1.16 }
          ] },
          { name: ".Trash", size: 0.86 }
        ] }
      ] },
      { name: "System", children: [
        { name: "Library", children: [
          { name: "Frameworks", children: [
            { name: "Core", size: 14.2 },
            { name: "UI", size: 9.31 },
            { name: "Media", size: 4.94 }
          ] },
          { name: "Extensions", children: [
            { name: "Driver A", size: 3.11 },
            { name: "Driver B", size: 3.92 }
          ] },
          { name: "Caches", size: 5.22 },
          { name: "Fonts", size: 11.25 },
          { name: "Preferences", size: 0.16 }
        ] },
        { name: "Updates", children: [
          { name: "Software Update A", children: [
            { name: "Package", size: 14.3 },
            { name: "Payload", size: 5.1 }
          ] },
          { name: "Software Update B", children: [
            { name: "Installer", size: 11.87 }
          ] }
        ] }
      ] },
      { name: "Applications", children: [
        { name: "Creative Suite", children: [
          { name: "Photo Editor", size: 9.84 },
          { name: "Video Editor", size: 14.12 },
          { name: "Design", size: 7.48 }
        ] },
        { name: "Games", children: [
          { name: "Game A", size: 12.01 },
          { name: "Game B", size: 4.45 },
          { name: "Game C", size: 3.26 }
        ] },
        { name: "Developer Tools", children: [
          { name: "Xcode", size: 9.4 },
          { name: "Terminal Tools", size: 2.42 }
        ] }
      ] },
      { name: "Library", children: [
        { name: "Frameworks", children: [
          { name: "Core A", size: 6.02 },
          { name: "Core B", size: 7.03 }
        ] },
        { name: "Caches", children: [
          { name: "Fonts", size: 1.1 },
          { name: "Icons", size: 6.31 }
        ] }
      ] },
      { name: "opt", children: [
        { name: "Homebrew", children: [
          { name: "Cellar", children: [
            { name: "python@3.12", size: 3.1 },
            { name: "node", size: 2.91 }
          ] },
          { name: "Caskroom", children: [
            { name: "casks", size: 3.35 }
          ] }
        ] },
        { name: "Other", children: [
          { name: "tools", size: 2.2 },
          { name: "local", size: 2.63 }
        ] }
      ] },
      { name: "private", children: [
        { name: "var", children: [
          { name: "folders", children: [
            { name: "a", size: 2.1 },
            { name: "b", size: 2.12 }
          ] },
          { name: "db", children: [
            { name: "sqlite", size: 2.88 }
          ] },
          { name: "log", children: [
            { name: "system.log", size: 2.01 }
          ] }
        ] },
        { name: "tmp", children: [
          { name: "scratch", size: 3.9 }
        ] }
      ] },
      { name: "usr", children: [
        { name: "lib", children: [
          { name: "libSystem", size: 4.2 },
          { name: "libc", size: 3.68 }
        ] },
        { name: "libexec", children: [
          { name: "helper", size: 1.9 },
          { name: "daemon", size: 1.54 }
        ] }
      ] },
      { name: "Other system files", children: [
        { name: "VM swap", size: 14.2 },
        { name: "sleepimage", size: 12.5 },
        { name: "Volumes metadata", size: 13.64 }
      ] }
    ]
  };

  // The app's palette, as [hue, saturation, brightness]
  var PALETTE = [
    [0.58, 0.72, 0.95], [0.47, 0.70, 0.80], [0.31, 0.62, 0.80], [0.14, 0.78, 0.96],
    [0.07, 0.78, 0.97], [0.99, 0.68, 0.94], [0.89, 0.58, 0.90], [0.75, 0.52, 0.92]
  ];
  var FILE_TINT = [0.6, 0.10, 0.60];
  var CX = 200, CY = 200, RADIUS = 190, INNER = RADIUS * 0.26;

  var svgNS = "http://www.w3.org/2000/svg";
  var orbit = document.getElementById("orbit");
  var svg = document.getElementById("sunburst");
  var center = document.getElementById("center");
  var crumbs = document.getElementById("crumbs");
  var legend = document.getElementById("legend");
  var legendTitle = document.getElementById("legend-title");
  var legendSub = document.getElementById("legend-sub");
  var scanView = document.getElementById("scan-view");
  var disksView = document.getElementById("disks-view");
  var backBtn = document.getElementById("back-disks");
  if (!orbit || !svg || !center || !crumbs || !legend || !legendTitle) return;

  // Sizes: a folder's size is the sum of its children
  function sizeOf(node) {
    if (!node.children) return node.size;
    node.size = node.children.reduce(function (sum, child) { return sum + sizeOf(child); }, 0);
    return node.size;
  }
  sizeOf(TREE);

  // Colours and parent links, set once
  function assign(node, top, depth) {
    var palette = 0;
    node.children.forEach(function (child, index) {
      child.parent = node;
      var hsb;
      if (!top) {
        hsb = PALETTE[palette % PALETTE.length];
        palette++;
      } else if (!child.children) {
        hsb = [top[0], FILE_TINT[1], FILE_TINT[2]];
      } else {
        var shade = index % 2 === 0 ? 0 : 0.07;
        hsb = [
          top[0],
          Math.max(0.25, top[1] - 0.07 * depth),
          Math.max(0.35, top[2] - 0.05 * depth - shade)
        ];
      }
      child.hsb = hsb;
      if (child.children) assign(child, top || hsb, depth + 1);
    });
  }
  assign(TREE, null, 0);

  function css(hsb, boost) {
    var h = hsb[0], s = Math.max(0, hsb[1] - (boost || 0) * 0.25), v = Math.min(1, hsb[2] + (boost || 0) * 0.12);
    var i = Math.floor(h * 6), f = h * 6 - i;
    var p = v * (1 - s), q = v * (1 - f * s), t = v * (1 - (1 - f) * s);
    var rgb = [[v, t, p], [q, v, p], [p, v, t], [p, q, v], [t, p, v], [v, p, q]][i % 6];
    return "rgb(" + rgb.map(function (c) { return Math.round(c * 255); }).join(",") + ")";
  }

  function fmt(gb) { return gb < 1 ? Math.round(gb * 1024) + " MB" : gb.toFixed(2) + " GB"; }
  function pct(part, whole) { return Math.round((part / whole) * 100) + "%"; }

  // Segments for everything under `focus`, with angles as fractions of the focus total
  function segmentsFor(focus) {
    var out = [];
    (function walk(node, start, depth) {
      var cursor = start;
      node.children.forEach(function (child) {
        var fraction = child.size / focus.size;
        if (fraction >= 0.003 && depth < 5) {
          out.push({ node: child, depth: depth, start: cursor, end: cursor + fraction });
        }
        if (child.children) walk(child, cursor, depth + 1);
        cursor += fraction;
      });
    })(focus, 0, 0);
    return out;
  }

  function point(r, fraction) {
    var a = fraction * 2 * Math.PI - Math.PI / 2;
    return [CX + r * Math.cos(a), CY + r * Math.sin(a)];
  }

  function arc(r0, r1, f0, f1) {
    if (f1 - f0 > 0.5) {
      var mid = (f0 + f1) / 2;
      return arc(r0, r1, f0, mid) + " " + arc(r0, r1, mid, f1);
    }
    var a = point(r1, f0), b = point(r1, f1), c = point(r0, f1), d = point(r0, f0);
    var p = function (x) { return x[0].toFixed(2) + " " + x[1].toFixed(2); };
    return "M" + p(a) + " A" + r1 + " " + r1 + " 0 0 1 " + p(b) +
      " L" + p(c) + " A" + r0 + " " + r0 + " 0 0 0 " + p(d) + " Z";
  }

  var state = { focus: TREE, hover: null };
  var paths = [];

  function isWithin(node, ancestor) {
    for (var n = node; n; n = n.parent) if (n === ancestor) return true;
    return false;
  }

  function render() {
    var focus = state.focus;
    var segments = segmentsFor(focus);
    var rings = segments.reduce(function (m, s) { return Math.max(m, s.depth + 1); }, 1);
    var ring = (RADIUS - INNER) / rings;

    svg.textContent = "";
    paths = [];
    segments.forEach(function (seg) {
      var r0 = INNER + seg.depth * ring;
      var r1 = r0 + ring - 1;
      var el = document.createElementNS(svgNS, "path");
      el.setAttribute("d", arc(r0, r1, seg.start, seg.end));
      el.setAttribute("class", "seg");
      el.setAttribute("fill", css(seg.node.hsb, 0));
      el.setAttribute("tabindex", "0");
      el.setAttribute("role", "button");
      el.setAttribute("aria-label", seg.node.name + ", " + fmt(seg.node.size) + ", " + pct(seg.node.size, focus.size));
      el.addEventListener("mouseenter", function () { setHover(seg.node); });
      el.addEventListener("focus", function () { setHover(seg.node); });
      el.addEventListener("mouseleave", function () { setHover(null); });
      el.addEventListener("blur", function () { setHover(null); });
      el.addEventListener("click", function () { choose(seg.node); });
      el.addEventListener("keydown", function (e) {
        if (e.key === "Enter" || e.key === " ") { e.preventDefault(); choose(seg.node); }
      });
      svg.appendChild(el);
      paths.push({ el: el, node: seg.node });
    });

    center.classList.toggle("can-up", !!focus.parent);
    renderCrumbs(focus);
    renderLegend(focus);
    setHover(null);
  }

  function choose(node) {
    if (!node.children) return;
    state.focus = node;
    render();
  }

  function renderCrumbs(focus) {
    var trail = [];
    for (var n = focus; n; n = n.parent) trail.unshift(n);
    crumbs.textContent = "";
    trail.forEach(function (node, i) {
      if (i > 0) {
        var sep = document.createElement("span");
        sep.textContent = "›";
        sep.setAttribute("aria-hidden", "true");
        crumbs.appendChild(sep);
      }
      var btn = document.createElement("button");
      btn.type = "button";
      btn.textContent = node.name;
      if (node === focus) btn.setAttribute("aria-current", "page");
      btn.addEventListener("click", function () { state.focus = node; render(); });
      crumbs.appendChild(btn);
    });
  }

  function renderLegend(focus) {
    legendTitle.textContent = focus.name;
    if (legendSub) legendSub.textContent = fmt(focus.size) + " · " + focus.children.length + (focus.children.length === 1 ? " item" : " items");
    legend.textContent = "";
    focus.children.slice().sort(function (a, b) { return b.size - a.size; }).forEach(function (child) {
      var li = document.createElement("li");
      var row = document.createElement(child.children ? "button" : "div");
      row.className = "row";
      if (child.children) row.type = "button";
      row.addEventListener("click", function () { choose(child); });
      row.addEventListener("mouseenter", function () { setHover(child); });
      row.addEventListener("mouseleave", function () { setHover(null); });
      row.addEventListener("focus", function () { setHover(child); });
      row.addEventListener("blur", function () { setHover(null); });
      row.dataset.name = child.name;

      var swatch = document.createElement("i");
      swatch.className = "swatch";
      swatch.style.background = css(child.hsb, 0);
      var icon = document.createElement("span");
      icon.className = "ico ico-folder";
      icon.setAttribute("aria-hidden", "true");
      var name = document.createElement("span");
      name.className = "name";
      name.textContent = child.name;
      var size = document.createElement("strong");
      size.textContent = fmt(child.size);

      row.appendChild(swatch);
      row.appendChild(icon);
      row.appendChild(name);
      row.appendChild(size);
      li.appendChild(row);
      legend.appendChild(li);
    });
  }

  function setHover(node) {
    state.hover = node;
    var focus = state.focus;
    paths.forEach(function (p) {
      var related = !node || isWithin(p.node, node) || isWithin(node, p.node);
      p.el.classList.toggle("dim", !related);
      p.el.classList.toggle("hot", node === p.node);
      p.el.setAttribute("fill", css(p.node.hsb, node === p.node ? 1 : 0));
    });
    legend.querySelectorAll(".row").forEach(function (row) {
      row.classList.toggle("hot", !!node && row.dataset.name === node.name);
    });
    var strong = center.querySelector("strong");
    var small = center.querySelector("small");
    // Like the app: the folder's name on top, its size underneath
    if (node) {
      strong.textContent = node.name;
      small.textContent = fmt(node.size);
      center.classList.add("is-hover");
    } else {
      strong.textContent = focus.name;
      small.textContent = fmt(focus.size);
      center.classList.remove("is-hover");
    }
  }

  // Clicking the centre goes back up one level
  function goUp() {
    if (state.focus.parent) { state.focus = state.focus.parent; render(); }
  }
  center.addEventListener("click", goUp);
  center.addEventListener("keydown", function (e) {
    if (e.key === "Enter" || e.key === " ") { e.preventDefault(); goUp(); }
  });

  // The Disks button returns to the disk list; a Scan button on a disk opens its scan at the top
  if (backBtn && scanView && disksView) {
    backBtn.addEventListener("click", function () {
      scanView.hidden = true;
      disksView.hidden = false;
    });
    disksView.querySelectorAll(".scan-btn").forEach(function (btn) {
      btn.addEventListener("click", function () {
        disksView.hidden = true;
        scanView.hidden = false;
        state.focus = TREE;
        render();
      });
    });
  }

  render();
})();
