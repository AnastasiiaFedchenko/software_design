import http from "k6/http";
import { check } from "k6";

export const BASE_URL = __ENV.BASE_URL || "http://localhost:8080";

export function login(jar, userId = 1, password = "pass1") {
  const payload = JSON.stringify({ id: userId, password: password });
  const res = http.post(`${BASE_URL}/api/auth/login`, payload, {
    headers: { "Content-Type": "application/json" },
    timeout: "2s",
    redirects: 0,
  });

  check(res, { "login ok": (r) => r.status === 200 });

  const cookie = res.cookies[".AspNetCore.Cookies"];
  if (cookie && cookie.length > 0) {
    jar.set(BASE_URL, ".AspNetCore.Cookies", cookie[0].value, {
      path: "/",
      secure: false,
    });
  }

  return res;
}
