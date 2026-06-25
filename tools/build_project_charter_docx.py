from copy import deepcopy
from pathlib import Path

from docx import Document
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.enum.table import WD_CELL_VERTICAL_ALIGNMENT
from docx.oxml import OxmlElement
from docx.oxml.ns import qn
from docx.shared import Pt


ROOT = Path(__file__).resolve().parents[1]
TEMPLATE = Path(r"C:\Users\augus\Downloads\Project Charterr (2).docx")
OUTPUT = ROOT / "Documentación General" / "Project_Charter_PokeRush_Formato.docx"


def set_font(run, size=10, bold=False):
    run.font.name = "Arial"
    run._element.rPr.rFonts.set(qn("w:ascii"), "Arial")
    run._element.rPr.rFonts.set(qn("w:hAnsi"), "Arial")
    run.font.size = Pt(size)
    run.bold = bold


def clear_cell(cell):
    for paragraph in cell.paragraphs:
        paragraph.clear()


def write_cell(cell, text, size=10, bold=False, align=WD_ALIGN_PARAGRAPH.LEFT):
    clear_cell(cell)
    paragraph = cell.paragraphs[0]
    paragraph.alignment = align
    paragraph.paragraph_format.space_after = Pt(0)
    paragraph.paragraph_format.line_spacing = 1.05
    for idx, line in enumerate(text.split("\n")):
        if idx:
            paragraph.add_run().add_break()
        run = paragraph.add_run(line)
        set_font(run, size=size, bold=bold)
    cell.vertical_alignment = WD_CELL_VERTICAL_ALIGNMENT.CENTER


def shade_cell(cell, fill):
    tc_pr = cell._tc.get_or_add_tcPr()
    shd = tc_pr.find(qn("w:shd"))
    if shd is None:
        shd = OxmlElement("w:shd")
        tc_pr.append(shd)
    shd.set(qn("w:fill"), fill)


def unique_cells(row):
    seen = set()
    cells = []
    for cell in row.cells:
        key = id(cell._tc)
        if key not in seen:
            seen.add(key)
            cells.append(cell)
    return cells


def set_row_text(table, row_idx, text, size=10, bold=False):
    for cell in unique_cells(table.rows[row_idx]):
        write_cell(cell, text, size=size, bold=bold)


def set_value_row(table, row_idx, label, value):
    cells = unique_cells(table.rows[row_idx])
    if len(cells) >= 2:
        write_cell(cells[0], label, bold=True)
        write_cell(cells[1], value)
    else:
        set_row_text(table, row_idx, f"{label}\n{value}")


def style_header_rows(table, row_indices):
    for idx in row_indices:
        for cell in unique_cells(table.rows[idx]):
            shade_cell(cell, "D9EAF7")
            for paragraph in cell.paragraphs:
                for run in paragraph.runs:
                    run.bold = True


def clone_last_row(table, count):
    base = table.rows[-1]._tr
    for _ in range(count):
        table._tbl.append(deepcopy(base))


def main():
    doc = Document(TEMPLATE)
    doc.paragraphs[0].text = "PROJECT CHARTER - POKERUSH"
    doc.paragraphs[0].alignment = WD_ALIGN_PARAGRAPH.CENTER
    for run in doc.paragraphs[0].runs:
        set_font(run, size=16, bold=True)

    table = doc.tables[0]

    set_row_text(table, 0, "1. Información general del proyecto", bold=True)
    set_value_row(table, 1, "Nombre del proyecto:", "PokeRush")
    set_value_row(table, 2, "Patrocinador:", "Directivos y docentes de la institución")
    set_value_row(
        table,
        3,
        "Impacto del proyecto:",
        "Proyecto integrador de desarrollo de videojuegos que aplica programación en C#, arquitectura MVC, base de datos SQLite, documentación técnica y trabajo colaborativo. Como producto, entrega un RPG 2D jugable con exploración, combates por turnos, inventario, progresión, eventos de historia y guardado local.",
    )

    set_row_text(table, 4, "2. Grupo del proyecto", bold=True)
    row5 = unique_cells(table.rows[5])
    if len(row5) >= 4:
        write_cell(row5[0], "")
        write_cell(row5[1], "Nombre", bold=True, align=WD_ALIGN_PARAGRAPH.CENTER)
        write_cell(row5[2], "Departamento / Rol", bold=True, align=WD_ALIGN_PARAGRAPH.CENTER)
        write_cell(row5[3], "E-mail", bold=True, align=WD_ALIGN_PARAGRAPH.CENTER)

    group_rows = [
        ("Project Manager:", "Equipo PokeRush", "Gestión del proyecto", ""),
        ("Team Members:", "Koyik", "DBA / Base de datos", ""),
        ("", "Brunello", "Developer", ""),
        ("", "Andrusiani", "Developer", ""),
    ]
    for offset, values in enumerate(group_rows, start=6):
        cells = unique_cells(table.rows[offset])
        for cell, text in zip(cells, values):
            write_cell(cell, text, bold=text.endswith(":"))
    for idx in range(10, 15):
        for cell in unique_cells(table.rows[idx]):
            write_cell(cell, "")

    set_row_text(
        table,
        15,
        "3. Stakeholders (interesados que pueden afectar al proyecto en forma positiva o negativa)",
        bold=True,
    )
    stakeholders = [
        "Directivos y docentes de la institución.",
        "Integrantes del equipo de desarrollo.",
        "Usuarios o jugadores de prueba.",
        "Responsables de evaluación del proyecto.",
        "Personas que revisen la build, documentación y presentación final.",
    ]
    for idx, text in enumerate(stakeholders, start=16):
        set_row_text(table, idx, text)

    set_row_text(table, 21, "4. Alcance del proyecto", bold=True)
    set_row_text(table, 22, "Propósitos del proyecto", bold=True)
    set_row_text(
        table,
        23,
        "Desarrollar un videojuego RPG 2D funcional en Unity y C#, con exploración, interacción con NPCs, equipo de Poken, inventario, combates por turnos contra bosses, eventos de historia y persistencia local mediante SQLite.",
    )
    set_row_text(table, 24, "Objetivos del proyecto (en relación al negocio encarado)", bold=True)
    set_row_text(
        table,
        25,
        "Crear un producto de entretenimiento digital demostrable; aplicar una arquitectura escalable para agregar contenido; implementar sistemas principales de gameplay; generar una build ejecutable; y entregar documentación técnica alineada con el estado real del proyecto.",
    )
    set_row_text(table, 26, "Objetivos entregables", bold=True)
    set_row_text(
        table,
        27,
        "Proyecto Unity completo, código fuente en C#, base de datos SQLite, documentación técnica, historias de usuario, diagramas, sistema de guardado/carga, combate por turnos, inventario, eventos de historia y build ejecutable del videojuego.",
    )
    set_row_text(table, 28, "Ámbito en el que se aplicará el uso de la aplicación", bold=True)
    set_row_text(
        table,
        29,
        "Ámbito educativo y de entretenimiento. El uso principal será local, mediante una build generada desde Unity. No requiere conexión permanente a internet porque el guardado se realiza en SQLite local.",
    )
    set_row_text(
        table,
        30,
        "Riesgos del proyecto (en relación al tiempo, entregas, alcance y público)",
        bold=True,
    )
    set_row_text(
        table,
        31,
        "Retrasos en funcionalidades principales; problemas al integrar sistemas; documentación desactualizada; errores al generar la build; fallas en guardado/carga; alcance demasiado amplio; dificultad de bosses mal balanceada; interfaz poco clara para usuarios nuevos.",
    )
    set_row_text(table, 32, "Restricciones", bold=True)
    set_row_text(
        table,
        33,
        "Tiempo limitado por calendario de entrega, disponibilidad de integrantes, recursos técnicos disponibles, necesidad de priorizar una versión funcional y dependencia del correcto funcionamiento del proyecto en Unity.",
    )
    set_row_text(table, 34, "Dependencias externas", bold=True)
    set_row_text(
        table,
        35,
        "El proyecto depende de Unity, paquetes 2D, SQLite-net, GitHub, computadoras personales, conexión a internet para sincronización del repositorio y coordinación con docentes o directivos para revisiones y entregas.",
    )
    set_row_text(
        table,
        36,
        "5. Estrategia de comunicación",
        bold=True,
    )
    set_row_text(
        table,
        37,
        "El equipo trabajará con comunicación frecuente por avance de tareas. Se realizarán revisiones semanales del estado general y revisiones especiales antes de cada entrega. Los cambios se compartirán por GitHub y la documentación se actualizará cuando se modifiquen funcionalidades relevantes. Los avances a patrocinadores o directivos se comunicarán en instancias de revisión, entrega o presentación, incluyendo documentación, capturas, diagramas o build ejecutable.",
    )
    set_row_text(table, 38, "6. Notas relevantes", bold=True)
    set_row_text(
        table,
        39,
        "Tecnologías principales: Unity, C#, SQLite, SQLite-net, Git, GitHub, Visual Studio/Rider, Unity UI, TextMeshPro, Input System, URP 2D, Tilemap y herramientas 2D. Las criaturas se denominan Poken. Los bosses se configuran con BossDefinition, los eventos con EventDefinition y el avance de historia mediante flags.",
    )

    style_header_rows(table, [0, 4, 15, 21, 22, 24, 26, 28, 30, 32, 34, 36, 38])

    for row in table.rows:
        for cell in unique_cells(row):
            for paragraph in cell.paragraphs:
                paragraph.paragraph_format.space_after = Pt(0)
                for run in paragraph.runs:
                    if run.font.size is None:
                        set_font(run, 10, run.bold)

    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    doc.save(OUTPUT)
    print(OUTPUT)


if __name__ == "__main__":
    main()
