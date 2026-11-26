// 与 Antigravity 宿主握手（MCP Apps 协议）。在普通浏览器中直接打开时跳过。
import { App } from '@modelcontextprotocol/ext-apps';

if (window.parent !== window) {
  try {
    const app = new App({ name: 'roxy-intro', version: '1.0.0' }, {});
    app.ontoolinput = () => {};
    app.ontoolresult = () => {};
    app.connect().catch(() => {});
  } catch (e) {
    /* 握手失败不影响动画播放 */
  }
}
