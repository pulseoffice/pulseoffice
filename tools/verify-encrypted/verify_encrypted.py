import os
import uno
from com.sun.star.beans import PropertyValue


def _property(name, value):
    item = PropertyValue()
    item.Name = name
    item.Value = value
    return item


def verify(*args):
    folder = os.environ["QO_SAMPLES"]
    password = os.environ["QO_PASSWORD"]
    desktop = XSCRIPTCONTEXT.getDesktop()
    report = []
    for name, flat in (("encrypted.ods", "calc8"), ("encrypted.odp", "impress8")):
        source = uno.systemPathToFileUrl(os.path.join(folder, name))
        try:
            document = desktop.loadComponentFromURL(source, "_blank", 0, (
                _property("Hidden", True), _property("Password", password)))
        except Exception as error:
            report.append("%s\tFAILED\t%s" % (name, error))
            continue
        if document is None:
            report.append("%s\tFAILED\tnot loaded" % name)
            continue
        plain = os.path.join(folder, "plain-" + name)
        document.storeToURL(uno.systemPathToFileUrl(plain), (_property("FilterName", flat),))
        pdf = os.path.join(folder, name + ".pdf")
        document.storeToURL(uno.systemPathToFileUrl(pdf), (_property("FilterName", "calc_pdf_Export" if flat == "calc8" else "impress_pdf_Export"),))
        document.close(True)
        report.append("%s\tOPENED" % name)
    with open(os.path.join(folder, "report.txt"), "w") as output:
        output.write("\n".join(report) + "\n")
    desktop.terminate()
