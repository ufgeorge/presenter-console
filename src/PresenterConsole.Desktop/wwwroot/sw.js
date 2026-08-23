const cacheName = "presenter-v13";
const appShell = ["/", "/index.html", "/app.css", "/app.js", "/NoSleep.min.js", "/manifest.webmanifest"];

self.addEventListener("install", event => {
  event.waitUntil(
    caches.open(cacheName)
      .then(cache => cache.addAll(appShell))
      .then(() => self.skipWaiting()));
});

self.addEventListener("activate", event => {
  event.waitUntil(
    caches.keys()
      .then(keys => Promise.all(
        keys.filter(key => key !== cacheName)
          .map(key => caches.delete(key))))
      .then(() => self.clients.claim()));
});

self.addEventListener("fetch", event => {
  event.respondWith(
    fetch(event.request)
      .then(response => {
        const copy = response.clone();
        caches.open(cacheName).then(cache => cache.put(event.request, copy));
        return response;
      })
      .catch(() => caches.match(event.request)));
});
