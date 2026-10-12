books = {
    1: "Konadu's autobiography",
    2: "Ali bab and the forty thieves",
    3: "Cinderealla "
}

print(
    """
    Books available
    1: "Konadu's autobiography",
    2: "Ali bab and the forty thieves",
    3: "Cinderealla "
"""
)


choice = int(input("Choose a book"))
print("Your choice is ", books[choice])
