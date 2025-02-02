import bpy
import traceback
from .server import Server
from .logger import Log
from .processing.importer import Importer

bl_info = {
    "name": "Rivals Porting",
    "description": "Import Server for Rivals Porting",
    "author": "Half, DeveloperChipmunk",
    "blender": (4, 2, 0),
    "version": (0, 1, 4),
    "category": "Import-Export",
}


def display_popup(text="Message", title="Information", icon='INFO'):
    def draw(self, context):
        self.layout.label(text=text)

    bpy.context.window_manager.popup_menu(draw, title=title, icon=icon)


def server_data_handler():
    if data := server.get_data():
        try:
            Importer.Import(data)
        except Exception as e:
            error_message = str(e)

            Log.error(f"An unhandled error occurred:")
            traceback.print_exc()

            display_popup(error_message, "An unhandled error occurred", "ERROR")

    return 0.01


def register():
    global server
    server = Server.create()
    server.start()

    bpy.app.timers.register(server_data_handler, persistent=True)


def unregister():
    server.shutdown()
