//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2006 Alan McGovern
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System.Xml;
using System.Text;
using MediaBrowser.Common.Net;

namespace Mono.Nat.Upnp
{
    internal abstract class MessageBase
    {
        protected UpnpNatDevice device;

        protected MessageBase(UpnpNatDevice device)
        {
            this.device = device;
        }

        protected HttpRequestOptions CreateRequest(string upnpMethod, string methodParameters)
        {
            var req = new HttpRequestOptions()
            {
                Url = $"http://{this.device.HostEndPoint}{this.device.ControlUrl}",
                EnableKeepAlive = false,
                RequestContentType = "text/xml",
                RequestContent = "<s:Envelope "
                + "xmlns:s=\"http://schemas.xmlsoap.org/soap/envelope/\" "
                + "s:encodingStyle=\"http://schemas.xmlsoap.org/soap/encoding/\">"
                + "<s:Body>"
                + "<u:" + upnpMethod + " "
                + "xmlns:u=\"" + device.ServiceType + "\">"
                + methodParameters
                + "</u:" + upnpMethod + ">"
                + "</s:Body>"
                + "</s:Envelope>\r\n\r\n"
            };

            req.RequestHeaders.Add("SOAPACTION", "\"" + device.ServiceType + "#" + upnpMethod + "\"");

            return req;
        }

        public abstract HttpRequestOptions Encode();

        public virtual string Method => "POST";

        protected void WriteFullElement(XmlWriter writer, string element, string value)
        {
            writer.WriteStartElement(element);
            writer.WriteString(value);
            writer.WriteEndElement();
        }

        protected XmlWriter CreateWriter(StringBuilder sb)
        {
            var settings = new XmlWriterSettings();
            settings.ConformanceLevel = ConformanceLevel.Fragment;
            return XmlWriter.Create(sb, settings);
        }
    }
}
