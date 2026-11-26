import './bridge.js';
import { startCelesteDirector } from './celeste_director.js';

if (document.readyState === 'loading') {
  document.addEventListener('DOMContentLoaded', startCelesteDirector);
} else {
  startCelesteDirector();
}
