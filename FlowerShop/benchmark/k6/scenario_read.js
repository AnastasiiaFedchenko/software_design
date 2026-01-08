import http from "k6/http";
import { check, sleep } from "k6";
import { BASE_URL } from "./common.js";

export const options = {
  vus: __ENV.VUS ? parseInt(__ENV.VUS, 10) : 20,
  duration: __ENV.DURATION || "30s",
  thresholds: {
    http_req_failed: ["rate<0.01"],
  },
  summaryTrendStats: ["avg", "min", "max", "p(50)", "p(75)", "p(90)", "p(95)", "p(99)"],
};

export default function () {
  const params = { timeout: "2s" };
  const listRes = http.get(`${BASE_URL}/api/products?skip=0&limit=20`, params);
  check(listRes, { "products list ok": (r) => r.status === 200 });

  const itemRes = http.get(`${BASE_URL}/api/products/70`, params);
  check(itemRes, { "product ok": (r) => r.status === 200 });

  sleep(1);
}
