// Frontend settings shared by every page.
window.APP_CONFIG = {
  // The API is served from the same origin.
  API_BASE: "https://commute360-7pdu.onrender.com",
  // The Maps JavaScript API key is visible to anyone who opens the page,
  // so RESTRICT it in Google Cloud Console (HTTP referrers + Maps JavaScript API only).
  GOOGLE_MAPS_API_KEY: "AIzaSyDqWs5onXNVQYKJ1KtQHHOcgCGzmWT25V8",

  MAP_ID: "184cc814228b9f1ec0242b5f",

  // Only used until the route's stops load
  DEFAULT_CENTER: { lat: 6.6745, lng: -1.5716 }
};