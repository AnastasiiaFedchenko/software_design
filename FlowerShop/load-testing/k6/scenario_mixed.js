import http from "k6/http";
import { check, sleep } from "k6";
import { BASE_URL, login } from "./common.js";

export const options = {
  vus: __ENV.VUS ? parseInt(__ENV.VUS, 10) : 10,
  duration: __ENV.DURATION || "30s",
  thresholds: {
    http_req_failed: ["rate<0.02"],
  },
  summaryTrendStats: ["avg", "min", "max", "p(50)", "p(75)", "p(90)", "p(95)", "p(99)"],
};

export default function () {
  const debug = __ENV.DEBUG === "1";
  const params = { headers: { "Content-Type": "application/json" }, timeout: "2s" };
  const jar = http.cookieJar();
  const userId = 100 + __VU;
  login(jar, userId, `pass${userId}`);

  const addRes = http.post(
    `${BASE_URL}/api/cart/items`,
    JSON.stringify({ productId: 70, quantity: 1 }),
    params
  );
  check(addRes, { "cart add ok": (r) => r.status === 200 });
  if (debug && addRes.status !== 200) {
    console.log(`cart add failed: status=${addRes.status} body=${addRes.body}`);
  }

  const cartRes = http.get(`${BASE_URL}/api/cart`);
  check(cartRes, { "cart get ok": (r) => r.status === 200 });

  const updateRes = http.patch(
    `${BASE_URL}/api/cart/products/70`,
    JSON.stringify({ newQuantity: 1 }),
    params
  );
  check(updateRes, { "cart update ok": (r) => r.status === 200 });
  if (debug && updateRes.status !== 200) {
    console.log(`cart update failed: status=${updateRes.status} body=${updateRes.body}`);
  }

  const removeRes = http.del(`${BASE_URL}/api/cart/products/70`, null, params);
  check(removeRes, { "cart remove ok": (r) => r.status === 200 });
  if (debug && removeRes.status !== 200) {
    console.log(`cart remove failed: status=${removeRes.status} body=${removeRes.body}`);
  }

  sleep(1);
}
