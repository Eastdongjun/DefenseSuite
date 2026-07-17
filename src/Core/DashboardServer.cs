using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Linq;

namespace CloudDefender.Core
{
    /// <summary>
    /// Embedded web dashboard for monitoring and managing defense.
    /// Serves HTML dashboard + JSON API on configurable port.
    /// </summary>
    public class DashboardServer
    {
        private HttpListener _listener;
        private Thread _thread;
        private int _port;
        private Installer _installer;
        public bool Running { get; private set; }

        public DashboardServer(int port, Installer installer)
        {
            _port = port;
            _installer = installer;
        }

        public void Start()
        {
            _thread = new Thread(Listen) { IsBackground = true };
            _thread.Start();
        }

        public void Stop()
        {
            Running = false;
            try { if (_listener != null) { try { _listener.Stop(); } catch { } } } catch { }
        }

        private void Listen()
        {
            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add("http://+:" + _port + "/");
                _listener.Start();
                Running = true;
            }
            catch
            {
                // Try localhost only
                try
                {
                    _listener = new HttpListener();
                    _listener.Prefixes.Add("http://localhost:" + _port + "/");
                    _listener.Prefixes.Add("http://127.0.0.1:" + _port + "/");
                    _listener.Start();
                    Running = true;
                }
                catch { return; }
            }

            while (Running)
            {
                try
                {
                    var ctx = _listener.GetContext();
                    HandleRequest(ctx);
                }
                catch { if (!Running) break; }
            }
        }

        private void HandleRequest(HttpListenerContext ctx)
        {
            string path = ctx.Request.Url.AbsolutePath.ToLower();
            byte[] response;

            if (path == "/" || path == "/index.html" || path == "/en" || path == "/zh")
                response = GetHtmlResponse(GetDashboardHtml(path));
            else if (path == "/api/status")
                response = GetJsonResponse(GetStatusJson());
            else if (path == "/api/attacks")
                response = GetJsonResponse(GetAttackLogJson());
            else if (path == "/api/rules")
                response = GetJsonResponse(GetRulesJson());
            else
                response = GetHtmlResponse(GetDashboardHtml(path));

            ctx.Response.ContentType = path.StartsWith("/api/") ? "application/json; charset=utf-8" : "text/html; charset=utf-8";
            ctx.Response.OutputStream.Write(response, 0, response.Length);
            ctx.Response.Close();
        }

        private byte[] GetHtmlResponse(string html)
        {
            return Encoding.UTF8.GetBytes(html);
        }

        private byte[] GetJsonResponse(string json)
        {
            return Encoding.UTF8.GetBytes(json);
        }

        
        // ========== Dashboard HTML (bilingual zh/en, base64-encoded) ==========
        private static readonly string DashboardHtmlB64 = @"PCFET0NUWVBFIGh0bWw+CjxodG1sPgo8aGVhZD4KPG1ldGEgY2hhcnNldD0nVVRGLTgnPgo8bWV0YSBuYW1lPSd2aWV3cG9ydCcgY29udGVudD0nd2lkdGg9ZGV2aWNlLXdpZHRoLGluaXRpYWwtc2NhbGU9MSc+Cjx0aXRsZT5DbG91ZERlZmVuZGVyPC90aXRsZT4KPHN0eWxlPgoqe21hcmdpbjowO3BhZGRpbmc6MDtib3gtc2l6aW5nOmJvcmRlci1ib3h9CmJvZHl7Zm9udC1mYW1pbHk6J1NlZ29lIFVJJywnTWljcm9zb2Z0IFlhSGVpJyxBcmlhbCxzYW5zLXNlcmlmO2JhY2tncm91bmQ6IzBhMGUxNztjb2xvcjojYzlkMWQ5O21pbi1oZWlnaHQ6MTAwdmh9Ci5oZWFkZXJ7YmFja2dyb3VuZDojMTYxYjIyO2JvcmRlci1ib3R0b206MXB4IHNvbGlkICMzMDM2M2Q7cGFkZGluZzoxNnB4IDI0cHg7ZGlzcGxheTpmbGV4O2FsaWduLWl0ZW1zOmNlbnRlcjtqdXN0aWZ5LWNvbnRlbnQ6c3BhY2UtYmV0d2Vlbn0KLmhlYWRlciBoMXtmb250LXNpemU6MjBweDtjb2xvcjojNThhNmZmfQoubGFuZy1idG57cGFkZGluZzo0cHggMTBweDtib3JkZXI6MXB4IHNvbGlkICMzMDM2M2Q7Ym9yZGVyLXJhZGl1czo0cHg7Y29sb3I6IzhiOTQ5ZTt0ZXh0LWRlY29yYXRpb246bm9uZTtmb250LXNpemU6MTJweDttYXJnaW4tbGVmdDo2cHg7Y3Vyc29yOnBvaW50ZXI7YmFja2dyb3VuZDp0cmFuc3BhcmVudH0KLmxhbmctYnRuLmFjdGl2ZXtiYWNrZ3JvdW5kOiMxZjZmZWI7Y29sb3I6I2ZmZjtib3JkZXItY29sb3I6IzFmNmZlYn0KLmdyaWR7ZGlzcGxheTpncmlkO2dyaWQtdGVtcGxhdGUtY29sdW1uczpyZXBlYXQoYXV0by1maXQsbWlubWF4KDI5MHB4LDFmcikpO2dhcDoxNnB4O3BhZGRpbmc6MjRweH0KLmNhcmR7YmFja2dyb3VuZDojMTYxYjIyO2JvcmRlcjoxcHggc29saWQgIzMwMzYzZDtib3JkZXItcmFkaXVzOjhweDtwYWRkaW5nOjIwcHh9Ci5jYXJkIGgze2ZvbnQtc2l6ZToxNHB4O2NvbG9yOiM4Yjk0OWU7bWFyZ2luLWJvdHRvbToxMnB4O3RleHQtdHJhbnNmb3JtOnVwcGVyY2FzZTtsZXR0ZXItc3BhY2luZzouNXB4fQouY2FyZCAudmFsdWV7Zm9udC1zaXplOjMycHg7Zm9udC13ZWlnaHQ6Ym9sZDtjb2xvcjojNThhNmZmfQoubW9kdWxlLWxpc3R7bGlzdC1zdHlsZTpub25lfS5tb2R1bGUtbGlzdCBsaXtkaXNwbGF5OmZsZXg7anVzdGlmeS1jb250ZW50OnNwYWNlLWJldHdlZW47cGFkZGluZzo4cHggMDtib3JkZXItYm90dG9tOjFweCBzb2xpZCAjMjEyNjJkO2ZvbnQtc2l6ZToxM3B4fQoubW9kdWxlLWxpc3QgLm9re2NvbG9yOiMzZmI5NTB9Lm1vZHVsZS1saXN0IC5lcnJ7Y29sb3I6I2Y4NTE0OX0KLnBvcnRze2Rpc3BsYXk6ZmxleDtmbGV4LXdyYXA6d3JhcDtnYXA6NnB4fS5wb3J0cyBzcGFue3BhZGRpbmc6M3B4IDhweDtib3JkZXItcmFkaXVzOjRweDtmb250LXNpemU6MTFweDtmb250LWZhbWlseTptb25vc3BhY2U7YmFja2dyb3VuZDojMGQ0MTlkO2NvbG9yOiM1OGE2ZmZ9LnBvcnRzIHNwYW4ub2Zme2JhY2tncm91bmQ6IzIxMjYyZDtjb2xvcjojNDg0ZjU4fQoudGFibGUtd3JhcHtvdmVyZmxvdy14OmF1dG99LnRhYmxle3dpZHRoOjEwMCU7Zm9udC1zaXplOjEycHg7Ym9yZGVyLWNvbGxhcHNlOmNvbGxhcHNlfS50YWJsZSB0aHtiYWNrZ3JvdW5kOiMyMTI2MmQ7cGFkZGluZzo4cHg7dGV4dC1hbGlnbjpsZWZ0O2ZvbnQtd2VpZ2h0Om5vcm1hbDtjb2xvcjojOGI5NDllfS50YWJsZSB0ZHtwYWRkaW5nOjhweDtib3JkZXItYm90dG9tOjFweCBzb2xpZCAjMjEyNjJkfS50YWJsZSB0cjpob3ZlcntiYWNrZ3JvdW5kOiMxYzIxMjh9Ci5mb290ZXJ7dGV4dC1hbGlnbjpjZW50ZXI7cGFkZGluZzoyMHB4O2NvbG9yOiM0ODRmNTg7Zm9udC1zaXplOjExcHh9Ci5yZWZyZXNoe2NvbG9yOiM1OGE2ZmY7Y3Vyc29yOnBvaW50ZXI7Zm9udC1zaXplOjEycHh9Cjwvc3R5bGU+CjwvaGVhZD4KPGJvZHk+CjxkaXYgY2xhc3M9J2hlYWRlcic+CjxkaXYgc3R5bGU9J2Rpc3BsYXk6ZmxleDthbGlnbi1pdGVtczpjZW50ZXI7Z2FwOjE2cHgnPgo8aDE+Q2xvdWREZWZlbmRlcjwvaDE+CjxkaXYgc3R5bGU9J2Rpc3BsYXk6ZmxleDtnYXA6NHB4Jz4KPGJ1dHRvbiBjbGFzcz0nbGFuZy1idG4nIG9uY2xpY2s9J3NldExhbmcoInpoIiknPuS4reaWhzwvYnV0dG9uPgo8YnV0dG9uIGNsYXNzPSdsYW5nLWJ0bicgb25jbGljaz0nc2V0TGFuZygiZW4iKSc+RU48L2J1dHRvbj4KPC9kaXY+CjwvZGl2Pgo8c3BhbiBzdHlsZT0ncGFkZGluZzo0cHggMTBweDtib3JkZXItcmFkaXVzOjEwcHg7Zm9udC1zaXplOjExcHg7YmFja2dyb3VuZDojMjM4NjM2O2NvbG9yOiNmZmYnIGlkPSd1cHRpbWUnPi08L3NwYW4+CjwvZGl2Pgo8ZGl2IGNsYXNzPSdncmlkJz4KPGRpdiBjbGFzcz0nY2FyZCc+CjxoMyBpZD0ndF9tb2R1bGVzJz5Nb2R1bGUgU3RhdHVzPC9oMz4KPHVsIGNsYXNzPSdtb2R1bGUtbGlzdCcgaWQ9J21vZHVsZXMnPjwvdWw+CjwvZGl2Pgo8ZGl2IGNsYXNzPSdjYXJkJz4KPGgzIGlkPSd0X3BvcnRzJz5Ib25leXBvdCBQb3J0czwvaDM+CjxkaXYgY2xhc3M9J3ZhbHVlJyBpZD0ncG9ydENvdW50Jz4tPC9kaXY+CjxkaXYgY2xhc3M9J3BvcnRzJyBpZD0ncG9ydExpc3QnPjwvZGl2Pgo8L2Rpdj4KPGRpdiBjbGFzcz0nY2FyZCc+CjxoMyBpZD0ndF9ydWxlcyc+RmlyZXdhbGwgUnVsZXM8L2gzPgo8ZGl2IGNsYXNzPSd2YWx1ZScgaWQ9J3J1bGVUb3RhbCc+LTwvZGl2Pgo8ZGl2IHN0eWxlPSdmb250LXNpemU6MTJweDttYXJnaW4tdG9wOjhweCc+CjxzcGFuIGlkPSd0X2RlZmVuZGVyJz5EZWZlbmRlcjwvc3Bhbj46IDxzcGFuIGlkPSdydWxlRGVmZW5kZXInPi08L3NwYW4+IHwKPHNwYW4gaWQ9J3RfaG9uZXlwb3QnPkhvbmV5cG90PC9zcGFuPjogPHNwYW4gaWQ9J3J1bGVIb25leXBvdCc+LTwvc3Bhbj4gfAo8c3BhbiBpZD0ndF93ZWJ0cmFwJz5XZWJUcmFwPC9zcGFuPjogPHNwYW4gaWQ9J3J1bGVXZWJ0cmFwJz4tPC9zcGFuPgo8L2Rpdj4KPC9kaXY+CjxkaXYgY2xhc3M9J2NhcmQnPgo8aDMgaWQ9J3RfYXR0YWNrcyc+UmVjZW50IEF0dGFja3MgKDI0aCk8L2gzPgo8ZGl2IGNsYXNzPSd2YWx1ZScgaWQ9J2F0dGFja0NvdW50Jz4tPC9kaXY+CjwvZGl2Pgo8L2Rpdj4KPGRpdiBjbGFzcz0nZ3JpZCc+CjxkaXYgY2xhc3M9J2NhcmQnIHN0eWxlPSdncmlkLWNvbHVtbjoxLy0xJz4KPGgzIGlkPSd0X2F0dGFja2xvZyc+QXR0YWNrIExvZzwvaDM+CjxkaXYgY2xhc3M9J3RhYmxlLXdyYXAnPjx0YWJsZSBjbGFzcz0ndGFibGUnPgo8dGhlYWQ+PHRyPjx0aCBpZD0ndF90aW1lJz5UaW1lPC90aD48dGggaWQ9J3RfdHlwZSc+VHlwZTwvdGg+PHRoPklQPC90aD48dGggaWQ9J3RfZGV0YWlsJz5EZXRhaWw8L3RoPjwvdHI+PC90aGVhZD4KPHRib2R5IGlkPSdhdHRhY2tUYWJsZSc+PC90Ym9keT4KPC90YWJsZT48L2Rpdj4KPC9kaXY+CjwvZGl2Pgo8ZGl2IGNsYXNzPSdmb290ZXInPgpDbG91ZERlZmVuZGVyIHYyLjAgLgo8c3BhbiBjbGFzcz0ncmVmcmVzaCcgb25jbGljaz0nbG9hZEFsbCgpJyBpZD0ndF9yZWZyZXNoJz5SZWZyZXNoPC9zcGFuPiAuCjxzcGFuIGlkPSd0X2F1dG9yZWZyZXNoJz5BdXRvLXJlZnJlc2ggMzBzPC9zcGFuPgo8L2Rpdj4KPHNjcmlwdD4KdmFyIEw9d2luZG93Lm5hdmlnYXRvci5sYW5ndWFnZS5zdGFydHNXaXRoKCd6aCcpPyd6aCc6J2VuJzsKdmFyIFQ9ewp6aDp7bW9kdWxlczon6Ziy5b6h5qih5Z2XJyxwb3J0czon6Jyc572Q56uv5Y+jJyxydWxlczon6Ziy54Gr5aKZ6KeE5YiZJyxhdHRhY2tzOifov5HmnJ/mlLvlh7soMjRoKScsYXR0YWNrbG9nOifmlLvlh7vml6Xlv5cnLHRpbWU6J+aXtumXtCcsdHlwZTon57G75Z6LJyxkZXRhaWw6J+ivpuaDhScsZGVmZW5kZXI6J0RlZmVuZGVyJyxob25leXBvdDonSG9uZXlwb3QnLHdlYnRyYXA6J1dlYlRyYXAnLHJlZnJlc2g6J+WIt+aWsCcsYXV0b3JlZnJlc2g6JzMw56eS6Ieq5Yqo5Yi35pawJyxhY3RpdmU6J+i/kOihjOS4rScsbm9hdHRhY2tzOifmmoLml6DmlLvlh7snLGxvYWRpbmc6J+WKoOi9veS4rS4uLid9LAplbjp7bW9kdWxlczonTW9kdWxlIFN0YXR1cycscG9ydHM6J0hvbmV5cG90IFBvcnRzJyxydWxlczonRmlyZXdhbGwgUnVsZXMnLGF0dGFja3M6J1JlY2VudCBBdHRhY2tzICgyNGgpJyxhdHRhY2tsb2c6J0F0dGFjayBMb2cnLHRpbWU6J1RpbWUnLHR5cGU6J1R5cGUnLGRldGFpbDonRGV0YWlsJyxkZWZlbmRlcjonRGVmZW5kZXInLGhvbmV5cG90OidIb25leXBvdCcsd2VidHJhcDonV2ViVHJhcCcscmVmcmVzaDonUmVmcmVzaCcsYXV0b3JlZnJlc2g6J0F1dG8tcmVmcmVzaCAzMHMnLGFjdGl2ZTonQWN0aXZlJyxub2F0dGFja3M6J05vIGF0dGFja3MgZGV0ZWN0ZWQnLGxvYWRpbmc6J0xvYWRpbmcuLi4nfQp9OwpmdW5jdGlvbiBzZXRMYW5nKGwpe0w9bDtsb2NhbFN0b3JhZ2Uuc2V0SXRlbSgnY2xkZWZfbGFuZycsbCk7YXBwbHlMYW5nKCk7bG9hZEFsbCgpfQpmdW5jdGlvbiB0KGspe3JldHVybiAoVFtMXXx8VC56aClba118fGt9CmZ1bmN0aW9uIGFwcGx5TGFuZygpewpkb2N1bWVudC5xdWVyeVNlbGVjdG9yQWxsKCdbaWRePXRfXScpLmZvckVhY2goZnVuY3Rpb24oZSl7ZS50ZXh0Q29udGVudD10KGUuaWQucmVwbGFjZSgndF8nLCcnKSl9KTsKZG9jdW1lbnQucXVlcnlTZWxlY3RvckFsbCgnLmxhbmctYnRuJykuZm9yRWFjaChmdW5jdGlvbihiKXtiLmNsYXNzTGlzdC50b2dnbGUoJ2FjdGl2ZScsKGIudGV4dENvbnRlbnQ9PSfkuK3mlocnJiZMPT0nemgnKXx8KGIudGV4dENvbnRlbnQ9PSdFTicmJkw9PSdlbicpKX0pCn0KdmFyIHNhdmVkPWxvY2FsU3RvcmFnZS5nZXRJdGVtKCdjbGRlZl9sYW5nJyk7aWYoc2F2ZWQpTD1zYXZlZDsKYXBwbHlMYW5nKCk7CmFzeW5jIGZ1bmN0aW9uIGxvYWRBbGwoKXsKZG9jdW1lbnQuZ2V0RWxlbWVudEJ5SWQoJ3VwdGltZScpLnRleHRDb250ZW50PXQoJ2FjdGl2ZScpOwp0cnl7dmFyIHI9YXdhaXQgZmV0Y2goJy9hcGkvc3RhdHVzJyk7dmFyIGQ9YXdhaXQgci5qc29uKCk7CnZhciBtPScnO2Zvcih2YXIgaT0wO2k8ZC5tb2R1bGVzLmxlbmd0aDtpKyspe3ZhciBzPWQubW9kdWxlc1tpXS5zdGF0dXM9PSdSdW5uaW5nJ3x8ZC5tb2R1bGVzW2ldLnN0YXR1cz09J1JlYWR5Jz90KCdhY3RpdmUnKTpkLm1vZHVsZXNbaV0uc3RhdHVzO20rPSc8bGk+JytkLm1vZHVsZXNbaV0ubmFtZSsnIDxzcGFuIGNsYXNzPScnKyhzPT10KCdhY3RpdmUnKT8nb2snOidlcnInKSsnJz4nK3MrJzwvc3Bhbj48L2xpPid9CmRvY3VtZW50LmdldEVsZW1lbnRCeUlkKCdtb2R1bGVzJykuaW5uZXJIVE1MPW18fCc8bGk+Jyt0KCdsb2FkaW5nJykrJzwvbGk+JzsKZG9jdW1lbnQuZ2V0RWxlbWVudEJ5SWQoJ3BvcnRDb3VudCcpLnRleHRDb250ZW50PWQucG9ydHMuYWN0aXZlKycvJytkLnBvcnRzLnRvdGFsOwp2YXIgcD0nJztmb3IodmFyIGk9MDtpPGQucG9ydHMubGlzdC5sZW5ndGg7aSsrKXtwKz0nPHNwYW4gY2xhc3M9JycnKyhkLnBvcnRzLmxpc3RbaV0uYWN0aXZlPycnOidvZmYnKSsnJz4nK2QucG9ydHMubGlzdFtpXS5udW1iZXIrJzwvc3Bhbj4nfQpkb2N1bWVudC5nZXRFbGVtZW50QnlJZCgncG9ydExpc3QnKS5pbm5lckhUTUw9cDsKZG9jdW1lbnQuZ2V0RWxlbWVudEJ5SWQoJ3J1bGVUb3RhbCcpLnRleHRDb250ZW50PWQucnVsZXMudG90YWw7CmRvY3VtZW50LmdldEVsZW1lbnRCeUlkKCdydWxlRGVmZW5kZXInKS50ZXh0Q29udGVudD1kLnJ1bGVzLmRlZmVuZGVyOwpkb2N1bWVudC5nZXRFbGVtZW50QnlJZCgncnVsZUhvbmV5cG90JykudGV4dENvbnRlbnQ9ZC5ydWxlcy5ob25leXBvdDsKZG9jdW1lbnQuZ2V0RWxlbWVudEJ5SWQoJ3J1bGVXZWJ0cmFwJykudGV4dENvbnRlbnQ9ZC5ydWxlcy53ZWJ0cmFwOwpkb2N1bWVudC5nZXRFbGVtZW50QnlJZCgnYXR0YWNrQ291bnQnKS50ZXh0Q29udGVudD1kLmF0dGFja3MudG90YWwKfWNhdGNoKGUpe30KdHJ5e3ZhciByPWF3YWl0IGZldGNoKCcvYXBpL2F0dGFja3MnKTt2YXIgZD1hd2FpdCByLmpzb24oKTsKdmFyIGE9Jyc7aWYoZC5sZW5ndGg9PTApYT0nPHRyPjx0ZCBjb2xzcGFuPTQ+Jyt0KCdub2F0dGFja3MnKSsnPC90ZD48L3RyPic7CmVsc2UgZm9yKHZhciBpPTA7aTxNYXRoLm1pbihkLmxlbmd0aCwyMCk7aSsrKXthKz0nPHRyPjx0ZD4nK2RbaV0udGltZSsnPC90ZD48dGQ+JytkW2ldLnR5cGUrJzwvdGQ+PHRkPicrZFtpXS5pcCsnPC90ZD48dGQgc3R5bGU9Zm9udC1zaXplOjExcHg+JytkW2ldLmRldGFpbCsnPC90ZD48L3RyPid9CmRvY3VtZW50LmdldEVsZW1lbnRCeUlkKCdhdHRhY2tUYWJsZScpLmlubmVySFRNTD1hfHwnPHRyPjx0ZCBjb2xzcGFuPTQ+Jyt0KCdsb2FkaW5nJykrJzwvdGQ+PC90cj4nCn1jYXRjaChlKXt9Cn0KbG9hZEFsbCgpO3NldEludGVydmFsKGxvYWRBbGwsMzAwMDApOwo8L3NjcmlwdD4KPC9ib2R5Pgo8L2h0bWw+";

        private string GetDashboardHtml(string path = "/")
        {
            string html = Encoding.UTF8.GetString(Convert.FromBase64String(DashboardHtmlB64));
            string lang = "zh";
            if (path != null && path.Contains("/en")) lang = "en";
            return html.Replace("var L=window.navigator.language.startsWith('zh')?'zh':'en'", "var L='" + lang + "'");
        }
// ========== JSON APIs ==========
        private string GetStatusJson()
        {
            var modules = new List<string>();
            foreach (var m in _installer.Modules)
            {
                modules.Add("{\"name\":\"" + m.Name + "\",\"status\":\"" + m.GetStatus() + "\"}");
            }

            int[] trapPorts = { 3389, 22, 23, 21, 5900, 9200, 11211, 27017, 8088, 5432, 5555, 8443 };
            var ports = new List<string>();
            int active = 0;
            var netstat = Shell("netstat", "-ano -p tcp");
            foreach (var p in trapPorts)
            {
                bool isActive = netstat.Contains("0.0.0.0:" + p + " ") && netstat.Contains("LISTENING");
                if (isActive) active++;
                ports.Add("{\"number\":" + p + ",\"active\":" + (isActive ? "true" : "false") + "}");
            }

            var rules = Shell("netsh", "advfirewall firewall show rule name=all");
            int defender = CountStr(rules, "DEFENDER_BLOCK_");
            int honeypot = CountStr(rules, "HONEYPOT_");
            int webtrap = CountStr(rules, "WEBTRAP_");

            // Count recent attacks
            int attackCount = 0;
            var paths = new[] {
                Path.Combine(_installer.InstallDir, "logs"),
                @"D:\Agent\Protect",
                @"C:\ProgramData\DefenseSuite\logs"
            };
            foreach (var dir in paths)
            {
                if (!Directory.Exists(dir)) continue;
                foreach (var f in Directory.GetFiles(dir, "*.log", SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        var fi = new FileInfo(f);
                        if (fi.LastWriteTime > DateTime.Now.AddDays(-1))
                        {
                            var content = File.ReadAllText(f);
                            attackCount += CountStr(content, "TRAP") + CountStr(content, "BAN");
                        }
                    }
                    catch { }
                }
            }

            return "{\"modules\":[" + string.Join(",", modules) + "]," +
                "\"ports\":{\"active\":" + active + ",\"total\":" + trapPorts.Length + ",\"list\":[" + string.Join(",", ports) + "]}," +
                "\"rules\":{\"total\":" + (defender + honeypot + webtrap) + ",\"defender\":" + defender + ",\"honeypot\":" + honeypot + ",\"webtrap\":" + webtrap + "}," +
                "\"attacks\":{\"total\":" + attackCount + "}}";
        }

        private string GetAttackLogJson()
        {
            var entries = new List<string>();
            var logDirs = new[] {
                Path.Combine(_installer.InstallDir, "logs"),
                @"D:\Agent\Protect",
                @"C:\ProgramData\DefenseSuite\logs"
            };

            foreach (var dir in logDirs)
            {
                if (!Directory.Exists(dir)) continue;
                foreach (var f in Directory.GetFiles(dir, "*.log", SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        var lines = File.ReadAllLines(f);
                        for (int i = lines.Length - 1; i >= 0 && entries.Count < 50; i--)
                        {
                            var line = lines[i];
                            if (line.Contains("TRAP") || line.Contains("BAN") || line.Contains("ALERT"))
                            {
                                string type = line.Contains("TRAP") ? "Trap" : line.Contains("BAN") ? "Ban" : "Alert";
                                string time = line.Length > 19 ? line.Substring(0, 19) : "";
                                string ip = "";
                                string detail = line;
                                var ipMatch = System.Text.RegularExpressions.Regex.Match(line, @"(\d+\.\d+\.\d+\.\d+)");
                                if (ipMatch.Success) ip = ipMatch.Value;

                                entries.Add("{\"time\":\"" + EscapeJson(time) + "\",\"type\":\"" + type + "\",\"ip\":\"" + ip + "\",\"detail\":\"" + EscapeJson(detail.Substring(0, Math.Min(detail.Length, 120))) + "\"}");
                            }
                        }
                    }
                    catch { }
                }
            }

            return "[" + string.Join(",", entries) + "]";
        }

        private string GetRulesJson()
        {
            var rules = Shell("netsh", "advfirewall firewall show rule name=all");
            var list = new List<string>();
            foreach (var line in rules.Split('\n'))
            {
                if (line.Contains("Rule Name:") &&
                    (line.Contains("DEFENDER_BLOCK_") || line.Contains("HONEYPOT_") || line.Contains("WEBTRAP_")))
                {
                    var name = line.Substring(line.IndexOf("Rule Name:") + 10).Trim();
                    list.Add("{\"name\":\"" + EscapeJson(name) + "\"}");
                }
            }
            return "{\"count\":" + list.Count + ",\"rules\":[" + string.Join(",", list.Take(100)) + "]}";
        }

        private static string Shell(string cmd, string args)
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo(cmd, args) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true };
                var p = System.Diagnostics.Process.Start(psi);
                var r = p.StandardOutput.ReadToEnd();
                p.WaitForExit(10000);
                return r;
            }
            catch { return ""; }
        }

        private static int CountStr(string text, string pattern)
        {
            int count = 0, i = 0;
            while ((i = text.IndexOf(pattern, i)) != -1) { count++; i += pattern.Length; }
            return count;
        }

        private static string EscapeJson(string s)
        {
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ").Replace("\r", "");
        }
    }
}
