// Stage slider and entrance motion for the DiskManager page.
// Without JavaScript the page shows everything: stage 1 is the default and .reveal only hides when `js` is set.
(function () {
  "use strict";

  var clauses = [
    "Choose a drive or folder to scan.",
    "Click through the sunburst to find the big items.",
    "Collect what you want gone and check the total."
  ];

  var slider = document.getElementById("stage");
  var clause = document.getElementById("stage-clause");
  var view = document.querySelector(".stage-view");
  var names = document.querySelectorAll("[data-stage-name]");

  function setStage(value) {
    var stage = String(value);
    view.setAttribute("data-stage", stage);
    clause.textContent = clauses[Number(stage)];
    slider.setAttribute("aria-valuetext", clauses[Number(stage)]);
    names.forEach(function (name) {
      name.classList.toggle("current", name.getAttribute("data-stage-name") === stage);
    });
  }

  if (slider && view && clause) {
    slider.addEventListener("input", function () { setStage(slider.value); });
    setStage(slider.value);
  }

  var reveals = document.querySelectorAll(".reveal");
  if ("IntersectionObserver" in window) {
    var observer = new IntersectionObserver(function (entries) {
      entries.forEach(function (entry) {
        if (entry.isIntersecting) {
          entry.target.classList.add("is-in");
          observer.unobserve(entry.target);
        }
      });
    }, { rootMargin: "0px 0px -8% 0px", threshold: 0.08 });
    reveals.forEach(function (el) { observer.observe(el); });
  } else {
    reveals.forEach(function (el) { el.classList.add("is-in"); });
  }
})();

