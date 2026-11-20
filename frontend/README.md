# CrisisAid React frontend

Run the API and SQL Server containers first, then:

```sh
npm install
npm run dev
```

Open http://localhost:5173. Vite proxies `/api` to `http://localhost:8080`.

Roles determine which pages and actions are visible. The backend performs independent authorization checks; hiding a UI element is not a security control. The browser stores the six-hour demo JWT in `sessionStorage`; a production deployment should move authentication to hardened, Secure/HttpOnly cookie flows with CSRF protections.

Mapping tiles are supplied by OpenStreetMap and geocoding is routed via the API to Nominatim. Use of public endpoints is limited by their terms and uptime; production requires a licensed provider / rate-policy review.
