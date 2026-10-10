const timeElement = document.querySelector("#time");
const greetingElement = document.querySelector("#greeting");
const searchForm = document.querySelector("#search-form");
const searchInput = document.querySelector("#search-input");

function updateClock() {
  const now = new Date();
  timeElement.textContent = now.toLocaleTimeString("en-GB", {
    hour: "2-digit",
    minute: "2-digit",
    second: "2-digit",
    hour12: false,
  });

  const hour = now.getHours();
  const greeting = hour < 12 ? "Good Morning" : hour < 18 ? "Good Afternoon" : "Good Evening";
  greetingElement.textContent = `${greeting}, Saad.`;
}

searchForm.addEventListener("submit", (event) => {
  event.preventDefault();
  const query = searchInput.value.trim();
  if (query) window.location.href = `https://www.google.com/search?q=${encodeURIComponent(query)}`;
});

updateClock();
setInterval(updateClock, 1000);