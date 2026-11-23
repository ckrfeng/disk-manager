// Scroll story and entrance motion for the DiskManager page.
// Without JavaScript the first step and its view show, and .reveal only hides when `js` is set.
(function () {
  "use strict";

  // The story: the step nearest the middle of the screen decides which app view shows
  var stage = document.querySelector(".story-stage");
  var steps = document.querySelectorAll(".story-step");
  if (stage && steps.length && "IntersectionObserver" in window) {
    var storyObserver = new IntersectionObserver(function (entries) {
      entries.forEach(function (entry) {
        if (!entry.isIntersecting) return;
        var current = entry.target;
        stage.setAttribute("data-active", current.getAttribute("data-step"));
        steps.forEach(function (step) { step.classList.toggle("is-current", step === current); });
      });
    }, { rootMargin: "-45% 0px -45% 0px", threshold: 0 });
    steps.forEach(function (step) { storyObserver.observe(step); });
  }

  // Sections rise in as they enter
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
